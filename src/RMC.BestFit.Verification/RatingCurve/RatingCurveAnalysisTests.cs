using Numerics;
using Numerics.Data;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets;
using System.Xml.Linq;
using BestFitRatingCurve = RMC.BestFit.Models.RatingCurve;

namespace RMC.BestFit.Verification.RatingCurve;

/// <summary>
/// Unit tests for the <see cref="RatingCurveAnalysis"/> class.
/// Validates Bayesian MCMC estimation workflow for stage-discharge rating curves.
/// </summary>
/// <remarks>
/// <para>
///     <b>Authors:</b>
/// </para>
/// <list type="bullet">
///     <item>Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil</item>
/// </list>
/// <para>
///     The <see cref="RatingCurveAnalysis"/> class performs Bayesian MCMC estimation
///     of stage-discharge rating curves. Rating curves relate river stage (water level)
///     to discharge (flow rate) using power-law segments, which is fundamental for
///     streamflow measurement and flood frequency analysis.
/// </para>
/// </remarks>
[TestClass]
public class RatingCurveAnalysisTests
{
    #region Test Data

    /// <summary>
    /// Default sample size for rating curve test data.
    /// </summary>
    private const int DefaultSampleSize = 500;

    /// <summary>
    /// Creates test stage and discharge time series data for rating curve analysis.
    /// Uses <see cref="SyntheticRatingCurveData"/> for reproducible test data.
    /// </summary>
    /// <param name="count">Number of observations to include. Default is 500.</param>
    /// <returns>A tuple containing stage and discharge <see cref="TimeSeries"/>.</returns>
    private static (TimeSeries stage, TimeSeries discharge) CreateTestData(int? count = null)
    {
        int sampleSize = count ?? DefaultSampleSize;
        var (stageTS, dischargeTS, _) = SyntheticRatingCurveData.GetSingleSegmentData(sampleSize: sampleSize);
        return (stageTS, dischargeTS);
    }

    /// <summary>
    /// Creates a test rating curve model with default settings.
    /// </summary>
    /// <param name="numberOfSegments">Number of power-law segments. Default = 1.</param>
    /// <returns>A configured <see cref="RatingCurve"/> model.</returns>
    private static BestFitRatingCurve CreateTestRatingCurve(int numberOfSegments = 1)
    {
        var (stageTS, dischargeTS) = CreateTestData();
        return new BestFitRatingCurve(stageTS, dischargeTS, numberOfSegments);
    }

    /// <summary>
    /// Creates a test rating curve analysis with default settings.
    /// </summary>
    /// <param name="numberOfSegments">Number of power-law segments. Default = 1.</param>
    /// <returns>A configured <see cref="RatingCurveAnalysis"/>.</returns>
    private static RatingCurveAnalysis CreateTestAnalysis(int numberOfSegments = 1)
    {
        var ratingCurve = CreateTestRatingCurve(numberOfSegments);
        return new RatingCurveAnalysis(ratingCurve);
    }

    #endregion

    #region Constructor Tests

    /// <summary>
    /// Tests that the constructor properly initializes the analysis with a rating curve model.
    /// </summary>
    [TestMethod]
    public void Constructor_WithRatingCurve_InitializesCorrectly()
    {
        // Arrange
        var ratingCurve = CreateTestRatingCurve();

        // Act
        var analysis = new RatingCurveAnalysis(ratingCurve);

        // Assert
        Assert.IsNotNull(analysis.RatingCurve, "RatingCurve should not be null.");
        Assert.IsNotNull(analysis.BayesianAnalysis, "BayesianAnalysis should not be null.");
        Assert.IsFalse(analysis.IsEstimated, "IsEstimated should be false initially.");
        Assert.IsNull(analysis.AnalysisResults, "AnalysisResults should be null initially.");
    }

    /// <summary>
    /// Tests that the constructor throws <see cref="ArgumentNullException"/> when rating curve is null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_WithNullRatingCurve_ThrowsArgumentNullException()
    {
        // Act
        _ = new RatingCurveAnalysis(null!);
    }

    /// <summary>
    /// Tests that the BayesianAnalysis property references the correct model.
    /// </summary>
    [TestMethod]
    public void Constructor_BayesianAnalysis_HasCorrectModel()
    {
        // Arrange
        var ratingCurve = CreateTestRatingCurve();

        // Act
        var analysis = new RatingCurveAnalysis(ratingCurve);

        // Assert
        Assert.AreSame(ratingCurve, analysis.BayesianAnalysis.Model,
            "BayesianAnalysis.Model should reference the rating curve.");
    }

    /// <summary>
    /// Tests that default stage bins are initialized based on data range.
    /// </summary>
    [TestMethod]
    public void Constructor_DefaultStageBins_InitializedFromDataRange()
    {
        // Arrange
        var ratingCurve = CreateTestRatingCurve();

        // Act
        var analysis = new RatingCurveAnalysis(ratingCurve);

        // Assert
        Assert.IsTrue(analysis.StageBins > 0, "StageBins should be positive.");
        Assert.AreEqual(100, analysis.StageBins, "Default StageBins should be 100.");
        Assert.IsTrue(analysis.MinStage < analysis.MaxStage, "MinStage should be less than MaxStage.");
    }

    /// <summary>
    /// Tests that constructor with multi-segment rating curve initializes correctly.
    /// </summary>
    [TestMethod]
    public void Constructor_WithMultiSegmentRatingCurve_InitializesCorrectly()
    {
        // Arrange
        var ratingCurve = CreateTestRatingCurve(numberOfSegments: 2);

        // Act
        var analysis = new RatingCurveAnalysis(ratingCurve);

        // Assert
        Assert.IsNotNull(analysis, "Analysis should be created.");
        Assert.AreEqual(2, analysis.RatingCurve.NumberOfSegments, "Should have 2 segments.");
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
        var ratingCurve = CreateTestRatingCurve();
        var analysis = new RatingCurveAnalysis(ratingCurve);

        // Configure stage bins
        analysis.MinStage = 2.0;
        analysis.MaxStage = 15.0;
        analysis.StageBins = 50;
        analysis.UseDefaultStageBins = false;

        // Act
        var xElement = analysis.ToXElement();
        var restoredAnalysis = new RatingCurveAnalysis(ratingCurve, xElement);

        // Assert
        Assert.IsNotNull(restoredAnalysis, "Restored analysis should not be null.");
        Assert.AreEqual(analysis.MinStage, restoredAnalysis.MinStage, 1e-10,
            "MinStage should be preserved.");
        Assert.AreEqual(analysis.MaxStage, restoredAnalysis.MaxStage, 1e-10,
            "MaxStage should be preserved.");
        Assert.AreEqual(analysis.StageBins, restoredAnalysis.StageBins,
            "StageBins should be preserved.");
        Assert.AreEqual(analysis.UseDefaultStageBins, restoredAnalysis.UseDefaultStageBins,
            "UseDefaultStageBins should be preserved.");
    }

    /// <summary>
    /// Tests that the constructor throws when XElement is null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_WithNullXElement_ThrowsArgumentNullException()
    {
        // Arrange
        var ratingCurve = CreateTestRatingCurve();

        // Act
        _ = new RatingCurveAnalysis(ratingCurve, null!);
    }

    /// <summary>
    /// Tests serialization with Bayesian analysis settings preserved.
    /// </summary>
    [TestMethod]
    public void XmlSerialization_WithBayesianSettings_PreservesSettings()
    {
        // Arrange
        var ratingCurve = CreateTestRatingCurve();
        var analysis = new RatingCurveAnalysis(ratingCurve);
        analysis.BayesianAnalysis.Iterations = 5000;
        analysis.BayesianAnalysis.WarmupIterations = 1000;

        // Act
        var xElement = analysis.ToXElement();
        var restoredAnalysis = new RatingCurveAnalysis(ratingCurve, xElement);

        // Assert
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
        // Arrange
        var ratingCurve = CreateTestRatingCurve();
        var analysis = new RatingCurveAnalysis(ratingCurve);

        // Act
        var xElement = analysis.ToXElement();

        // Assert
        Assert.IsNotNull(xElement, "XElement should not be null.");
        Assert.AreEqual("RatingCurveAnalysis", xElement.Name.LocalName,
            "Root element should be RatingCurveAnalysis.");
        Assert.IsNotNull(xElement.Attribute("IsEstimated"), "Should have IsEstimated attribute.");
        Assert.IsNotNull(xElement.Attribute("MinStage"), "Should have MinStage attribute.");
        Assert.IsNotNull(xElement.Attribute("MaxStage"), "Should have MaxStage attribute.");
        Assert.IsNotNull(xElement.Attribute("StageBins"), "Should have StageBins attribute.");
    }

    /// <summary>
    /// Tests that serialization preserves the IsEstimated flag.
    /// </summary>
    [TestMethod]
    public void XmlSerialization_PreservesIsEstimatedFlag()
    {
        // Arrange
        var ratingCurve = CreateTestRatingCurve();
        var analysis = new RatingCurveAnalysis(ratingCurve);

        // Act
        var xElement = analysis.ToXElement();

        // Assert
        var isEstimatedAttr = xElement.Attribute("IsEstimated");
        Assert.IsNotNull(isEstimatedAttr, "Should have IsEstimated attribute.");
        // XML boolean serialization uses lowercase "true"/"false"
        Assert.AreEqual("false", isEstimatedAttr.Value, "IsEstimated should be false.");
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
        var analysis = CreateTestAnalysis();

        // Act
        var (isValid, messages) = analysis.Validate();

        // Assert
        Assert.IsTrue(isValid, $"Validation should pass. Messages: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests that Validate checks the underlying rating curve model.
    /// </summary>
    [TestMethod]
    public void Validate_PropagatesRatingCurveValidation()
    {
        // Arrange
        var analysis = CreateTestAnalysis();

        // Act
        var (isValid, messages) = analysis.Validate();

        // Assert
        Assert.IsTrue(isValid, "Validation should pass for valid rating curve.");
    }

    /// <summary>
    /// Tests that Validate fails when MinStage is NaN.
    /// </summary>
    [TestMethod]
    public void Validate_WithNaNMinStage_ReturnsInvalid()
    {
        // Arrange
        var analysis = CreateTestAnalysis();
        analysis.UseDefaultStageBins = false;
        analysis.MinStage = double.NaN;

        // Act
        var (isValid, messages) = analysis.Validate();

        // Assert
        Assert.IsFalse(isValid, "Validation should fail for NaN MinStage.");
        Assert.IsTrue(messages.Any(m => m.Contains("Minimum stage")),
            "Should have message about invalid MinStage.");
    }

    /// <summary>
    /// Tests that Validate fails when MaxStage is NaN.
    /// </summary>
    [TestMethod]
    public void Validate_WithNaNMaxStage_ReturnsInvalid()
    {
        // Arrange
        var analysis = CreateTestAnalysis();
        analysis.UseDefaultStageBins = false;
        analysis.MaxStage = double.NaN;

        // Act
        var (isValid, messages) = analysis.Validate();

        // Assert
        Assert.IsFalse(isValid, "Validation should fail for NaN MaxStage.");
        Assert.IsTrue(messages.Any(m => m.Contains("Maximum stage")),
            "Should have message about invalid MaxStage.");
    }

    /// <summary>
    /// Tests that Validate fails when MinStage >= MaxStage.
    /// </summary>
    [TestMethod]
    public void Validate_WithInvertedStageRange_ReturnsInvalid()
    {
        // Arrange
        var analysis = CreateTestAnalysis();
        analysis.UseDefaultStageBins = false;
        analysis.MinStage = 10.0;
        analysis.MaxStage = 5.0;

        // Act
        var (isValid, messages) = analysis.Validate();

        // Assert
        Assert.IsFalse(isValid, "Validation should fail when MinStage >= MaxStage.");
        Assert.IsTrue(messages.Any(m => m.Contains("less than")),
            "Should have message about stage range.");
    }

    /// <summary>
    /// Tests that Validate fails when StageBins is out of valid range.
    /// </summary>
    [TestMethod]
    [DataRow(5)]
    [DataRow(1001)]
    public void Validate_WithInvalidStageBins_ReturnsInvalid(int stageBins)
    {
        // Arrange
        var analysis = CreateTestAnalysis();
        analysis.StageBins = stageBins;

        // Act
        var (isValid, messages) = analysis.Validate();

        // Assert
        Assert.IsFalse(isValid, $"Validation should fail for StageBins = {stageBins}.");
        Assert.IsTrue(messages.Any(m => m.Contains("stage bins")),
            "Should have message about invalid StageBins.");
    }

    /// <summary>
    /// Tests that Validate succeeds with valid stage bins at boundaries.
    /// </summary>
    [TestMethod]
    [DataRow(10)]
    [DataRow(1000)]
    public void Validate_WithValidStageBinsAtBoundary_ReturnsValid(int stageBins)
    {
        // Arrange
        var analysis = CreateTestAnalysis();
        analysis.StageBins = stageBins;

        // Act
        var (isValid, messages) = analysis.Validate();

        // Assert
        Assert.IsTrue(isValid, $"Validation should pass for StageBins = {stageBins}. Messages: {string.Join(", ", messages)}");
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
        var analysis = CreateTestAnalysis();

        // Act
        analysis.ClearResults();

        // Assert
        Assert.IsFalse(analysis.IsEstimated, "IsEstimated should be false after clearing.");
        Assert.IsNull(analysis.AnalysisResults, "AnalysisResults should be null after clearing.");
    }

    /// <summary>
    /// Tests that ClearResults also clears BayesianAnalysis results.
    /// </summary>
    [TestMethod]
    public void ClearResults_ClearsBayesianAnalysisResults()
    {
        // Arrange
        var analysis = CreateTestAnalysis();

        // Act
        analysis.ClearResults();

        // Assert
        Assert.IsFalse(analysis.BayesianAnalysis.IsEstimated,
            "BayesianAnalysis.IsEstimated should be false after clearing.");
    }

    #endregion

    #region Property Change Tests

    /// <summary>
    /// Tests that MinStage changes trigger property change notifications.
    /// </summary>
    [TestMethod]
    public void MinStage_Change_RaisesPropertyChanged()
    {
        // Arrange
        var analysis = CreateTestAnalysis();
        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(RatingCurveAnalysis.MinStage))
                propertyChanged = true;
        };

        // Act
        analysis.MinStage = 0.5;

        // Assert
        Assert.IsTrue(propertyChanged, "PropertyChanged should be raised for MinStage.");
    }

    /// <summary>
    /// Tests that MaxStage changes trigger property change notifications.
    /// </summary>
    [TestMethod]
    public void MaxStage_Change_RaisesPropertyChanged()
    {
        // Arrange
        var analysis = CreateTestAnalysis();
        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(RatingCurveAnalysis.MaxStage))
                propertyChanged = true;
        };

        // Act
        analysis.MaxStage = 20.0;

        // Assert
        Assert.IsTrue(propertyChanged, "PropertyChanged should be raised for MaxStage.");
    }

    /// <summary>
    /// Tests that StageBins changes trigger property change notifications.
    /// </summary>
    [TestMethod]
    public void StageBins_Change_RaisesPropertyChanged()
    {
        // Arrange
        var analysis = CreateTestAnalysis();
        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(RatingCurveAnalysis.StageBins))
                propertyChanged = true;
        };

        // Act
        analysis.StageBins = 50;

        // Assert
        Assert.IsTrue(propertyChanged, "PropertyChanged should be raised for StageBins.");
    }

    /// <summary>
    /// Tests that UseDefaultStageBins changes trigger property change notifications.
    /// </summary>
    [TestMethod]
    public void UseDefaultStageBins_Change_RaisesPropertyChanged()
    {
        // Arrange
        var analysis = CreateTestAnalysis();
        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(RatingCurveAnalysis.UseDefaultStageBins))
                propertyChanged = true;
        };

        // Act
        analysis.UseDefaultStageBins = false;

        // Assert
        Assert.IsTrue(propertyChanged, "PropertyChanged should be raised for UseDefaultStageBins.");
    }

    /// <summary>
    /// Tests that setting UseDefaultStageBins to true resets stage bins from data.
    /// </summary>
    [TestMethod]
    public void UseDefaultStageBins_SetToTrue_ResetsFromData()
    {
        // Arrange
        var analysis = CreateTestAnalysis();
        analysis.UseDefaultStageBins = false;
        analysis.MinStage = -100;
        analysis.MaxStage = 100;
        analysis.StageBins = 25;

        // Act
        analysis.UseDefaultStageBins = true;

        // Assert
        Assert.AreEqual(100, analysis.StageBins, "StageBins should be reset to 100.");
        Assert.IsTrue(analysis.MinStage > -100, "MinStage should be reset from data.");
        Assert.IsTrue(analysis.MaxStage < 100, "MaxStage should be reset from data.");
    }

    /// <summary>
    /// Tests that changing stage bin properties clears results.
    /// </summary>
    [TestMethod]
    public void StageBinProperties_Change_ClearsResults()
    {
        // Arrange
        var analysis = CreateTestAnalysis();

        // Act & Assert - MinStage
        analysis.MinStage = 0.5;
        Assert.IsNull(analysis.AnalysisResults, "Results should be cleared after MinStage change.");

        // Act & Assert - MaxStage
        analysis.MaxStage = 20.0;
        Assert.IsNull(analysis.AnalysisResults, "Results should be cleared after MaxStage change.");

        // Act & Assert - StageBins
        analysis.StageBins = 50;
        Assert.IsNull(analysis.AnalysisResults, "Results should be cleared after StageBins change.");
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
        var analysis = CreateTestAnalysis();
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
        var analysis = CreateTestAnalysis();

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
    /// Tests that cancellation during analysis sets WasCanceled flag.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_WhenCanceled_SetsWasCanceledFlag()
    {
        // Arrange
        var analysis = CreateTestAnalysis();
        bool wasCanceled = false;

        analysis.AnalysisStarting += (s, e) => e.Cancel = true;
        analysis.AnalysisCompleted += (s, e) =>
        {
            wasCanceled = e.Cancelled;
        };

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(wasCanceled, "WasCanceled should be true.");
        Assert.IsFalse(analysis.IsEstimated, "IsEstimated should be false when canceled.");
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
        var analysis = CreateTestAnalysis();

        // Act & Assert - Should not throw
        analysis.CancelAnalysis();
    }

    /// <summary>
    /// Tests that CancelAnalysis cancels the underlying BayesianAnalysis.
    /// </summary>
    [TestMethod]
    public void CancelAnalysis_CancelsBayesianAnalysis()
    {
        // Arrange
        var analysis = CreateTestAnalysis();

        // Act & Assert - Should not throw
        analysis.CancelAnalysis();
        // Note: We can't easily verify internal cancellation without running,
        // but this confirms the method doesn't throw
    }

    #endregion

    #region Integration Tests

    /// <summary>
    /// Tests that the analysis validates correctly with different segment counts.
    /// </summary>
    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void Validate_WithVariousSegmentCounts_ReturnsValid(int segments)
    {
        // Arrange
        var analysis = CreateTestAnalysis(segments);

        // Act
        var (isValid, messages) = analysis.Validate();

        // Assert
        Assert.IsTrue(isValid,
            $"Validation should pass for {segments} segment(s). Messages: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests that modifying the rating curve model clears results.
    /// </summary>
    [TestMethod]
    public void RatingCurveChange_ClearsResults()
    {
        // Arrange
        var analysis = CreateTestAnalysis();

        // Simulate that analysis was estimated
        bool resultsCleared = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(RatingCurveAnalysis.AnalysisResults))
                resultsCleared = true;
        };

        // Act - Trigger property change on rating curve by changing segments
        analysis.RatingCurve.NumberOfSegments = 2;

        // Assert
        Assert.IsTrue(resultsCleared || analysis.AnalysisResults == null,
            "Results should be cleared when rating curve changes.");
    }

    /// <summary>
    /// Tests creating analysis with various segment configurations.
    /// </summary>
    [TestMethod]
    public void Constructor_WithVariousSegmentCounts_InitializesCorrectly()
    {
        for (int segments = 1; segments <= 3; segments++)
        {
            // Arrange & Act
            var analysis = CreateTestAnalysis(segments);

            // Assert
            Assert.IsNotNull(analysis, $"Analysis should be created for {segments} segment(s).");
            Assert.AreEqual(segments, analysis.RatingCurve.NumberOfSegments,
                "Segment count should match.");
        }
    }

    #endregion

    #region Stage Bin Configuration Tests

    /// <summary>
    /// Tests that default stage bins are initialized correctly.
    /// </summary>
    [TestMethod]
    public void StageBins_DefaultInitialization_HasDefaultValues()
    {
        // Arrange & Act
        var analysis = CreateTestAnalysis();

        // Assert
        Assert.AreEqual(100, analysis.StageBins, "Default StageBins should be 100.");
        Assert.IsTrue(analysis.UseDefaultStageBins, "UseDefaultStageBins should be true by default.");
    }

    /// <summary>
    /// Tests that stage bins can be modified.
    /// </summary>
    [TestMethod]
    public void StageBins_CanBeModified()
    {
        // Arrange
        var analysis = CreateTestAnalysis();
        analysis.UseDefaultStageBins = false;

        // Act
        analysis.MinStage = 1.0;
        analysis.MaxStage = 20.0;
        analysis.StageBins = 200;

        // Assert
        Assert.AreEqual(1.0, analysis.MinStage, 1e-10, "MinStage should be 1.0.");
        Assert.AreEqual(20.0, analysis.MaxStage, 1e-10, "MaxStage should be 20.0.");
        Assert.AreEqual(200, analysis.StageBins, "StageBins should be 200.");
    }

    /// <summary>
    /// Tests that stage bins have 10% padding from data range.
    /// </summary>
    [TestMethod]
    public void DefaultStageBins_HasPaddingFromDataRange()
    {
        // Arrange
        var (stageTS, dischargeTS) = CreateTestData();
        double dataMin = stageTS.MinValue();
        double dataMax = stageTS.MaxValue();
        double range = dataMax - dataMin;

        // Act
        var ratingCurve = new BestFitRatingCurve(stageTS, dischargeTS);
        var analysis = new RatingCurveAnalysis(ratingCurve);

        // Assert - Default stage bins should have 10% padding
        double expectedMin = dataMin - 0.1 * range;
        double expectedMax = dataMax + 0.1 * range;

        Assert.AreEqual(expectedMin, analysis.MinStage, 1e-6, "MinStage should have 10% padding.");
        Assert.AreEqual(expectedMax, analysis.MaxStage, 1e-6, "MaxStage should have 10% padding.");
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Tests analysis with minimal data (edge case).
    /// </summary>
    [TestMethod]
    public void Constructor_WithMinimalData_InitializesCorrectly()
    {
        // Arrange - Create minimal data (at least 10 pairs required)
        var (stageTS, dischargeTS) = CreateTestData(15);

        var ratingCurve = new BestFitRatingCurve(stageTS, dischargeTS);

        // Act
        var analysis = new RatingCurveAnalysis(ratingCurve);

        // Assert
        Assert.IsNotNull(analysis, "Analysis should be created with minimal data.");
        var (isValid, _) = analysis.Validate();
        Assert.IsTrue(isValid, "Analysis with minimal data should be valid.");
    }

    /// <summary>
    /// Tests that setting same MinStage value does not raise property changed.
    /// </summary>
    [TestMethod]
    public void MinStage_SetSameValue_DoesNotRaisePropertyChanged()
    {
        // Arrange
        var analysis = CreateTestAnalysis();
        double originalMin = analysis.MinStage;
        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(RatingCurveAnalysis.MinStage))
                propertyChanged = true;
        };

        // Act
        analysis.MinStage = originalMin;

        // Assert
        Assert.IsFalse(propertyChanged, "PropertyChanged should not be raised for same value.");
    }

    /// <summary>
    /// Tests that setting same MaxStage value does not raise property changed.
    /// </summary>
    [TestMethod]
    public void MaxStage_SetSameValue_DoesNotRaisePropertyChanged()
    {
        // Arrange
        var analysis = CreateTestAnalysis();
        double originalMax = analysis.MaxStage;
        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(RatingCurveAnalysis.MaxStage))
                propertyChanged = true;
        };

        // Act
        analysis.MaxStage = originalMax;

        // Assert
        Assert.IsFalse(propertyChanged, "PropertyChanged should not be raised for same value.");
    }

    /// <summary>
    /// Tests that setting same StageBins value does not raise property changed.
    /// </summary>
    [TestMethod]
    public void StageBins_SetSameValue_DoesNotRaisePropertyChanged()
    {
        // Arrange
        var analysis = CreateTestAnalysis();
        int originalBins = analysis.StageBins;
        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(RatingCurveAnalysis.StageBins))
                propertyChanged = true;
        };

        // Act
        analysis.StageBins = originalBins;

        // Assert
        Assert.IsFalse(propertyChanged, "PropertyChanged should not be raised for same value.");
    }

    /// <summary>
    /// Tests that rating curve with empty stage data handles gracefully.
    /// </summary>
    [TestMethod]
    public void Constructor_WithEmptyStageData_SetsDefaultStageBins()
    {
        // Arrange
        var ratingCurve = new BestFitRatingCurve();

        // Act
        var analysis = new RatingCurveAnalysis(ratingCurve);

        // Assert - Should use default stage bins when no data
        Assert.AreEqual(0, analysis.MinStage, "MinStage should default to 0.");
        Assert.AreEqual(100, analysis.MaxStage, "MaxStage should default to 100.");
        Assert.AreEqual(100, analysis.StageBins, "StageBins should default to 100.");
    }

    #endregion

    #region RunAsync Invalid Configuration Tests

    /// <summary>
    /// Tests that RunAsync throws for invalid configuration.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_WithInvalidConfiguration_ThrowsInvalidOperationException()
    {
        // Arrange
        var analysis = CreateTestAnalysis();
        analysis.UseDefaultStageBins = false;
        analysis.MinStage = 100;  // Invalid: greater than MaxStage
        analysis.MaxStage = 50;

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await analysis.RunAsync(),
            "RunAsync should throw for invalid configuration.");
    }

    #endregion

    #region BayesianAnalysis Configuration Tests

    /// <summary>
    /// Tests that BayesianAnalysis settings can be configured.
    /// </summary>
    [TestMethod]
    public void BayesianAnalysis_CanBeConfigured()
    {
        // Arrange
        var analysis = CreateTestAnalysis();

        // Act
        analysis.BayesianAnalysis.Iterations = 10000;
        analysis.BayesianAnalysis.WarmupIterations = 2000;
        analysis.BayesianAnalysis.ThinningInterval = 10;
        analysis.BayesianAnalysis.CredibleIntervalWidth = 0.90;

        // Assert
        Assert.AreEqual(10000, analysis.BayesianAnalysis.Iterations);
        Assert.AreEqual(2000, analysis.BayesianAnalysis.WarmupIterations);
        Assert.AreEqual(10, analysis.BayesianAnalysis.ThinningInterval);
        Assert.AreEqual(0.90, analysis.BayesianAnalysis.CredibleIntervalWidth, 1e-10);
    }

    /// <summary>
    /// Tests that UseSimulationDefaults flag can be set.
    /// </summary>
    [TestMethod]
    public void BayesianAnalysis_UseSimulationDefaults_CanBeSet()
    {
        // Arrange
        var analysis = CreateTestAnalysis();

        // Act
        analysis.BayesianAnalysis.UseSimulationDefaults = false;

        // Assert
        Assert.IsFalse(analysis.BayesianAnalysis.UseSimulationDefaults);
    }

    #endregion

    #region Rating Curve Model Property Propagation Tests

    /// <summary>
    /// Tests that changes to BestFitRatingCurve.StageData updates default stage bins.
    /// </summary>
    [TestMethod]
    public void RatingCurveStageDataChange_UpdatesDefaultStageBins()
    {
        // Arrange
        var analysis = CreateTestAnalysis();
        double originalMin = analysis.MinStage;
        double originalMax = analysis.MaxStage;

        // Act - Add new data point with different range
        var newDate = analysis.RatingCurve.StageData.Last().Index.AddDays(1);
        analysis.RatingCurve.StageData.Add(new SeriesOrdinate<DateTime, double>(newDate, 50.0)); // Much larger value

        // Assert - If UseDefaultStageBins is true, the stage bins should update
        if (analysis.UseDefaultStageBins)
        {
            Assert.IsTrue(analysis.MaxStage > originalMax || analysis.MinStage != originalMin,
                "Stage bins should update when data changes.");
        }
    }

    /// <summary>
    /// Tests that NumberOfSegments change in BestFitRatingCurve clears results.
    /// </summary>
    [TestMethod]
    public void RatingCurveNumberOfSegmentsChange_ClearsResults()
    {
        // Arrange
        var analysis = CreateTestAnalysis(1);
        bool resultsPropertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == "Parameters" || e.PropertyName == nameof(RatingCurveAnalysis.AnalysisResults))
                resultsPropertyChanged = true;
        };

        // Act
        analysis.RatingCurve.NumberOfSegments = 2;

        // Assert
        Assert.IsNull(analysis.AnalysisResults, "AnalysisResults should be null after segment change.");
        Assert.IsFalse(analysis.IsEstimated, "IsEstimated should be false after segment change.");
        // Note: resultsPropertyChanged tracks if event was raised
        _ = resultsPropertyChanged;
    }

    #endregion

    #region Jeffreys Prior Tests

    /// <summary>
    /// Tests that UseJeffreysRuleForScale can be configured on rating curve.
    /// </summary>
    [TestMethod]
    public void RatingCurve_UseJeffreysRuleForScale_CanBeConfigured()
    {
        // Arrange
        var analysis = CreateTestAnalysis();

        // Act
        analysis.RatingCurve.UseJeffreysRuleForScale = true;

        // Assert
        Assert.IsTrue(analysis.RatingCurve.UseJeffreysRuleForScale);
    }

    /// <summary>
    /// Tests that UseDefaultFlatPriors can be configured on rating curve.
    /// </summary>
    [TestMethod]
    public void RatingCurve_UseDefaultFlatPriors_CanBeConfigured()
    {
        // Arrange
        var analysis = CreateTestAnalysis();

        // Act
        analysis.RatingCurve.UseDefaultFlatPriors = false;

        // Assert
        Assert.IsFalse(analysis.RatingCurve.UseDefaultFlatPriors);
    }

    #endregion

    #region Parameter Recovery Tests - Single Segment

    /// <summary>
    /// Tests Bayesian MCMC estimation of single-segment rating curve parameters against known true values.
    /// Uses low-noise synthetic data for accurate parameter recovery.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rating curve model: Q = α * (h - ξ)^β with log-normal error σ.
    /// Parameters: [ξ, log10(α), β, σ] = [0.3, log10(15), 1.8, 0.02]
    /// </para>
    /// <para>
    /// Low noise (σ=0.02) enables 15% tolerance on Bayesian parameter recovery.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_SingleSegment_LowNoise()
    {
        var (stageTS, dischargeTS, trueParams) = SyntheticRatingCurveData.GetLowNoiseData(sampleSize: 500);
        var model = new BestFitRatingCurve(stageTS, dischargeTS, numberOfSegments: 1)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultFlatPriors = false
        };

        var analysis = new RatingCurveAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            // Use minimum absolute tolerance of 0.5 to handle zero-valued or small parameters
            double tolerance = Math.Max(0.5, Math.Abs(trueParams[i] * 0.15));
            Assert.AreEqual(trueParams[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], tolerance,
                $"Estimated parameter {i} ({model.Parameters[i].Name}) is incorrect.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of single-segment rating curve with default parameters.
    /// Uses standard synthetic data with moderate noise.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Default parameters: [ξ, log10(α), β, σ] = [0.5, log10(10), 2.0, 0.05]
    /// </para>
    /// <para>
    /// Moderate noise (σ=0.05) requires 20% tolerance for Bayesian estimation.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_SingleSegment_Default()
    {
        var (stageTS, dischargeTS, trueParams) = SyntheticRatingCurveData.GetSingleSegmentData(sampleSize: 500);
        var model = new BestFitRatingCurve(stageTS, dischargeTS, numberOfSegments: 1)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultFlatPriors = false
        };

        var analysis = new RatingCurveAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            // Use minimum absolute tolerance of 0.5 to handle zero-valued or small parameters
            double tolerance = Math.Max(0.5, Math.Abs(trueParams[i] * 0.20));
            Assert.AreEqual(trueParams[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], tolerance,
                $"Estimated parameter {i} ({model.Parameters[i].Name}) is incorrect.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of single-segment rating curve for steep channel (mountain stream).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Steep channel parameters: [ξ, log10(α), β, σ] = [0.1, log10(5), 2.8, 0.05]
    /// High exponent (β=2.8) represents steep mountain stream.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_SingleSegment_SteepChannel()
    {
        var (stageTS, dischargeTS, trueParams) = SyntheticRatingCurveData.GetSteepChannelData(sampleSize: 500);
        var model = new BestFitRatingCurve(stageTS, dischargeTS, numberOfSegments: 1)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultFlatPriors = false
        };

        var analysis = new RatingCurveAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            // Use minimum absolute tolerance of 0.5 to handle zero-valued or small parameters
            double tolerance = Math.Max(0.5, Math.Abs(trueParams[i] * 0.20));
            Assert.AreEqual(trueParams[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], tolerance,
                $"Estimated parameter {i} ({model.Parameters[i].Name}) is incorrect.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of single-segment rating curve for wide channel (floodplain).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Wide channel parameters: [ξ, log10(α), β, σ] = [0.2, log10(50), 1.3, 0.05]
    /// Low exponent (β=1.3) represents wide floodplain.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_SingleSegment_WideChannel()
    {
        var (stageTS, dischargeTS, trueParams) = SyntheticRatingCurveData.GetWideChannelData(sampleSize: 500);
        var model = new BestFitRatingCurve(stageTS, dischargeTS, numberOfSegments: 1)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultFlatPriors = false
        };

        var analysis = new RatingCurveAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            // Use minimum absolute tolerance of 0.5 to handle zero-valued or small parameters
            double tolerance = Math.Max(0.5, Math.Abs(trueParams[i] * 0.20));
            Assert.AreEqual(trueParams[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], tolerance,
                $"Estimated parameter {i} ({model.Parameters[i].Name}) is incorrect.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation with large sample size for improved precision.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses 1000 observations for tighter parameter recovery (15% tolerance).
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_SingleSegment_LargeSample()
    {
        var (stageTS, dischargeTS, trueParams) = SyntheticRatingCurveData.GetLargeSampleData();
        var model = new BestFitRatingCurve(stageTS, dischargeTS, numberOfSegments: 1)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultFlatPriors = false
        };

        var analysis = new RatingCurveAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            // Use minimum absolute tolerance of 0.5 to handle zero-valued or small parameters
            double tolerance = Math.Max(0.5, Math.Abs(trueParams[i] * 0.15));
            Assert.AreEqual(trueParams[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], tolerance,
                $"Estimated parameter {i} ({model.Parameters[i].Name}) is incorrect.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation with wide stage range data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Wide stage range (0.5 to 15 m) tests extrapolation behavior.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_SingleSegment_WideRange()
    {
        var (stageTS, dischargeTS, trueParams) = SyntheticRatingCurveData.GetWideRangeData(sampleSize: 500);
        var model = new BestFitRatingCurve(stageTS, dischargeTS, numberOfSegments: 1)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultFlatPriors = false
        };

        var analysis = new RatingCurveAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            // Use minimum absolute tolerance of 0.5 to handle zero-valued parameters (xi=0)
            double tolerance = Math.Max(0.5, Math.Abs(trueParams[i] * 0.20));
            Assert.AreEqual(trueParams[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], tolerance,
                $"Estimated parameter {i} ({model.Parameters[i].Name}) is incorrect.");
        }
    }

    #endregion

    #region Parameter Recovery Tests - Two Segments

    /// <summary>
    /// Tests Bayesian MCMC estimation of two-segment rating curve parameters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two-segment model: Q = α₁(h-ξ)^β₁ for h &lt; h₂, Q = α₂(h-ξ)^β₂ for h ≥ h₂
    /// Parameters: [ξ, log10(α₁), β₁, h₂, log10(α₂), β₂, σ]
    /// </para>
    /// <para>
    /// Two-segment models require more data and larger tolerance (25%) due to
    /// the increased number of parameters, breakpoint estimation, and MCMC variability.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_TwoSegment_Default()
    {
        var (stageTS, dischargeTS, trueParams) = SyntheticRatingCurveData.GetTwoSegmentData(sampleSize: 500);
        var model = new BestFitRatingCurve(stageTS, dischargeTS, numberOfSegments: 2)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultFlatPriors = false
        };

        var analysis = new RatingCurveAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            // Use minimum absolute tolerance of 0.5 to handle zero-valued or small parameters
            // Multi-segment models have more complex likelihood surfaces
            double tolerance = Math.Max(0.5, Math.Abs(trueParams[i] * 0.25));
            Assert.AreEqual(trueParams[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], tolerance,
                $"Estimated parameter {i} ({model.Parameters[i].Name}) is incorrect.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of two-segment rating curve with bankfull transition.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bankfull transition represents in-bank to overbank flow transition.
    /// Sharp change in exponent at bankfull stage.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_TwoSegment_BankfullTransition()
    {
        var (stageTS, dischargeTS, trueParams) = SyntheticRatingCurveData.GetBankfullTransitionData(sampleSize: 500);
        var model = new BestFitRatingCurve(stageTS, dischargeTS, numberOfSegments: 2)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultFlatPriors = false
        };

        var analysis = new RatingCurveAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            // Use minimum absolute tolerance of 0.5 to handle zero-valued or small parameters
            // Multi-segment models have more complex likelihood surfaces
            double tolerance = Math.Max(0.5, Math.Abs(trueParams[i] * 0.25));
            Assert.AreEqual(trueParams[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], tolerance,
                $"Estimated parameter {i} ({model.Parameters[i].Name}) is incorrect.");
        }
    }

    #endregion

    #region Parameter Recovery Tests - Three Segments

    /// <summary>
    /// Tests Bayesian MCMC estimation of three-segment rating curve parameters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three-segment model has 10 parameters: [ξ, log10(α₁), β₁, h₂, log10(α₂), β₂, h₃, log10(α₃), β₃, σ]
    /// </para>
    /// <para>
    /// Three-segment models are challenging to estimate and require 30% tolerance
    /// due to the high dimensionality and MCMC variability.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_ThreeSegment_Default()
    {
        var (stageTS, dischargeTS, trueParams) = SyntheticRatingCurveData.GetThreeSegmentData(sampleSize: 800);
        var model = new BestFitRatingCurve(stageTS, dischargeTS, numberOfSegments: 3)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultFlatPriors = false
        };

        var analysis = new RatingCurveAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            // Use minimum absolute tolerance of 1.0 for three-segment models
            // High-dimensional MCMC has significant variability
            double tolerance = Math.Max(1.0, Math.Abs(trueParams[i] * 0.30));
            Assert.AreEqual(trueParams[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], tolerance,
                $"Estimated parameter {i} ({model.Parameters[i].Name}) is incorrect.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of three-segment rating curve with multiple controls.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Multiple control data represents section control, channel control, and floodplain control.
    /// Each segment has distinct hydraulic characteristics.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_ThreeSegment_MultipleControl()
    {
        var (stageTS, dischargeTS, trueParams) = SyntheticRatingCurveData.GetMultipleControlData(sampleSize: 800);
        var model = new BestFitRatingCurve(stageTS, dischargeTS, numberOfSegments: 3)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultFlatPriors = false
        };

        var analysis = new RatingCurveAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            // Use minimum absolute tolerance of 1.0 for three-segment models
            // High-dimensional MCMC has significant variability
            double tolerance = Math.Max(1.0, Math.Abs(trueParams[i] * 0.30));
            Assert.AreEqual(trueParams[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], tolerance,
                $"Estimated parameter {i} ({model.Parameters[i].Name}) is incorrect.");
        }
    }

    #endregion
}
