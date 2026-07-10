using Numerics.Data;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using BestFitRatingCurve = RMC.BestFit.Models.RatingCurve;
using NumericsTimeSeries = Numerics.Data.TimeSeries;

namespace RMC.BestFit.Tests.RatingCurve;

/// <summary>
/// Programmatic unit tests for the <c>RatingCurveAnalysis</c> class.
/// </summary>
/// <remarks>
/// <para>
///     <b>Authors:</b>
/// </para>
/// <list type="bullet">
///     <item>Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil</item>
/// </list>
/// <para>
/// Tests cover construction, configuration, validation, serialization, property
/// notifications, and event wiring. Bayesian MCMC parameter-recovery tests live
/// in <c>RMC.BestFit.Verification</c>.
/// </para>
/// </remarks>
[TestClass]
public class RatingCurveAnalysisTests
{
    #region Inline Test Data

    // Synthetic stage values (1-10 ft) generated via BestFitRatingCurve.GenerateSyntheticData
    // produce a moderate-range fixture suitable for property-level testing.
    private static readonly double[] s_trueParams = { 0.5, 1.0, 2.0, 0.05 }; // [ξ, log10(α), β, σ]

    /// <summary>
    /// Builds a single-segment synthetic stage/discharge dataset using the model's
    /// <c>RMC.BestFit.Models.RatingCurve.GenerateSyntheticData</c> method.
    /// True parameters: [ξ, log10(α), β, σ] = [0.5, log10(10), 2.0, 0.05].
    /// </summary>
    /// <param name="sampleSize">Number of observations. Default = 200.</param>
    /// <returns>A tuple of (stage, discharge) NumericsTimeSeries.</returns>
    private static (Numerics.Data.TimeSeries Stage, Numerics.Data.TimeSeries Discharge)
        MakeSingleSegmentData(int sampleSize = 200)
    {
        var seedModel = new RMC.BestFit.Models.RatingCurve { UseDefaultFlatPriors = false };
        seedModel.SetParameterValues(s_trueParams);
        return seedModel.GenerateSyntheticData(sampleSize, minStage: 1.0, maxStage: 10.0, seed: 12345);
    }

    /// <summary>
    /// Creates a configured <c>RMC.BestFit.Models.RatingCurve</c> with default settings.
    /// </summary>
    private static RMC.BestFit.Models.RatingCurve CreateTestRatingCurve(int numberOfSegments = 1)
    {
        var (stageTS, dischargeTS) = MakeSingleSegmentData();
        return new RMC.BestFit.Models.RatingCurve(stageTS, dischargeTS, numberOfSegments);
    }

    /// <summary>
    /// Creates a configured <c>RatingCurveAnalysis</c> wrapping a fresh model.
    /// </summary>
    private static RatingCurveAnalysis CreateTestAnalysis(int numberOfSegments = 1)
    {
        return new RatingCurveAnalysis(CreateTestRatingCurve(numberOfSegments));
    }

    #endregion

    #region Constructor Tests

    /// <summary>
    /// Constructor with rating curve initializes all properties.
    /// </summary>
    [TestMethod]
    public void Constructor_WithRatingCurve_InitializesCorrectly()
    {
        var ratingCurve = CreateTestRatingCurve();

        var analysis = new RatingCurveAnalysis(ratingCurve);

        Assert.IsNotNull(analysis.RatingCurve, "RatingCurve should not be null.");
        Assert.IsNotNull(analysis.BayesianAnalysis, "BayesianAnalysis should not be null.");
        Assert.IsFalse(analysis.IsEstimated, "IsEstimated should be false initially.");
        Assert.IsNull(analysis.AnalysisResults, "AnalysisResults should be null initially.");
    }

    /// <summary>
    /// Constructor throws <c>ArgumentNullException</c> when rating curve is null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_WithNullRatingCurve_ThrowsArgumentNullException()
    {
        _ = new RatingCurveAnalysis(null!);
    }

    /// <summary>
    /// BayesianAnalysis property references the supplied rating curve model.
    /// </summary>
    [TestMethod]
    public void Constructor_BayesianAnalysis_HasCorrectModel()
    {
        var ratingCurve = CreateTestRatingCurve();

        var analysis = new RatingCurveAnalysis(ratingCurve);

        Assert.AreSame(ratingCurve, analysis.BayesianAnalysis.Model,
            "BayesianAnalysis.Model should reference the rating curve.");
    }

    /// <summary>
    /// Default stage bins are initialized from the data range.
    /// </summary>
    [TestMethod]
    public void Constructor_DefaultStageBins_InitializedFromDataRange()
    {
        var ratingCurve = CreateTestRatingCurve();

        var analysis = new RatingCurveAnalysis(ratingCurve);

        Assert.IsTrue(analysis.StageBins > 0, "StageBins should be positive.");
        Assert.AreEqual(100, analysis.StageBins, "Default StageBins should be 100.");
        Assert.IsTrue(analysis.MinStage < analysis.MaxStage, "MinStage should be less than MaxStage.");
    }

    /// <summary>
    /// Constructor with a multi-segment rating curve initializes correctly.
    /// </summary>
    [TestMethod]
    public void Constructor_WithMultiSegmentRatingCurve_InitializesCorrectly()
    {
        var ratingCurve = CreateTestRatingCurve(numberOfSegments: 2);

        var analysis = new RatingCurveAnalysis(ratingCurve);

        Assert.IsNotNull(analysis, "Analysis should be created.");
        Assert.AreEqual(2, analysis.RatingCurve.NumberOfSegments, "Should have 2 segments.");
    }

    /// <summary>
    /// Constructor with various segment counts initializes correctly.
    /// </summary>
    [TestMethod]
    public void Constructor_WithVariousSegmentCounts_InitializesCorrectly()
    {
        for (int segments = 1; segments <= 3; segments++)
        {
            var analysis = CreateTestAnalysis(segments);

            Assert.IsNotNull(analysis, $"Analysis should be created for {segments} segment(s).");
            Assert.AreEqual(segments, analysis.RatingCurve.NumberOfSegments,
                "Segment count should match.");
        }
    }

    /// <summary>
    /// Analysis with minimal data (≥10 pairs) initializes correctly.
    /// </summary>
    [TestMethod]
    public void Constructor_WithMinimalData_InitializesCorrectly()
    {
        var (stageTS, dischargeTS) = MakeSingleSegmentData(sampleSize: 15);
        var ratingCurve = new RMC.BestFit.Models.RatingCurve(stageTS, dischargeTS);

        var analysis = new RatingCurveAnalysis(ratingCurve);

        Assert.IsNotNull(analysis, "Analysis should be created with minimal data.");
        var (isValid, _) = analysis.Validate();
        Assert.IsTrue(isValid, "Analysis with minimal data should be valid.");
    }

    /// <summary>
    /// Rating curve with empty stage data uses fallback default stage bins.
    /// </summary>
    [TestMethod]
    public void Constructor_WithEmptyStageData_SetsDefaultStageBins()
    {
        var ratingCurve = new RMC.BestFit.Models.RatingCurve();

        var analysis = new RatingCurveAnalysis(ratingCurve);

        Assert.AreEqual(0, analysis.MinStage, "MinStage should default to 0.");
        Assert.AreEqual(100, analysis.MaxStage, "MaxStage should default to 100.");
        Assert.AreEqual(100, analysis.StageBins, "StageBins should default to 100.");
    }

    #endregion

    #region XML Serialization Tests

    /// <summary>
    /// XML round-trip preserves stage-bin configuration.
    /// </summary>
    [TestMethod]
    public void XmlSerialization_RoundTrip_PreservesConfiguration()
    {
        var ratingCurve = CreateTestRatingCurve();
        var analysis = new RatingCurveAnalysis(ratingCurve)
        {
            MinStage = 2.0,
            MaxStage = 15.0,
            StageBins = 50,
            UseDefaultStageBins = false
        };

        var xElement = analysis.ToXElement();
        var restoredAnalysis = new RatingCurveAnalysis(ratingCurve, xElement);

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
    /// Constructor throws when the supplied XElement is null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_WithNullXElement_ThrowsArgumentNullException()
    {
        var ratingCurve = CreateTestRatingCurve();

        _ = new RatingCurveAnalysis(ratingCurve, null!);
    }

    /// <summary>
    /// XML round-trip preserves Bayesian analysis settings.
    /// </summary>
    [TestMethod]
    public void XmlSerialization_WithBayesianSettings_PreservesSettings()
    {
        var ratingCurve = CreateTestRatingCurve();
        var analysis = new RatingCurveAnalysis(ratingCurve);
        analysis.BayesianAnalysis.Iterations = 5000;
        analysis.BayesianAnalysis.WarmupIterations = 1000;

        var xElement = analysis.ToXElement();
        var restoredAnalysis = new RatingCurveAnalysis(ratingCurve, xElement);

        Assert.AreEqual(5000, restoredAnalysis.BayesianAnalysis.Iterations,
            "Iterations should be preserved.");
        Assert.AreEqual(1000, restoredAnalysis.BayesianAnalysis.WarmupIterations,
            "WarmupIterations should be preserved.");
    }

    /// <summary>
    /// ToXElement creates valid XML structure with expected attributes.
    /// </summary>
    [TestMethod]
    public void ToXElement_CreatesValidXmlStructure()
    {
        var ratingCurve = CreateTestRatingCurve();
        var analysis = new RatingCurveAnalysis(ratingCurve);

        var xElement = analysis.ToXElement();

        Assert.IsNotNull(xElement, "XElement should not be null.");
        Assert.AreEqual("RatingCurveAnalysis", xElement.Name.LocalName,
            "Root element should be RatingCurveAnalysis.");
        Assert.IsNotNull(xElement.Attribute("IsEstimated"), "Should have IsEstimated attribute.");
        Assert.IsNotNull(xElement.Attribute("MinStage"), "Should have MinStage attribute.");
        Assert.IsNotNull(xElement.Attribute("MaxStage"), "Should have MaxStage attribute.");
        Assert.IsNotNull(xElement.Attribute("StageBins"), "Should have StageBins attribute.");
    }

    /// <summary>
    /// XML serialization preserves the IsEstimated flag.
    /// </summary>
    [TestMethod]
    public void XmlSerialization_PreservesIsEstimatedFlag()
    {
        var ratingCurve = CreateTestRatingCurve();
        var analysis = new RatingCurveAnalysis(ratingCurve);

        var xElement = analysis.ToXElement();

        var isEstimatedAttr = xElement.Attribute("IsEstimated");
        Assert.IsNotNull(isEstimatedAttr, "Should have IsEstimated attribute.");
        // XML boolean serialization uses lowercase "true"/"false"
        Assert.AreEqual("false", isEstimatedAttr.Value, "IsEstimated should be false.");
    }

    #endregion

    #region Validation Tests

    /// <summary>
    /// Validate returns valid for a properly configured analysis.
    /// </summary>
    [TestMethod]
    public void Validate_WithValidConfiguration_ReturnsValid()
    {
        var analysis = CreateTestAnalysis();

        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Validation should pass. Messages: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Validate propagates the underlying rating curve model validation.
    /// </summary>
    [TestMethod]
    public void Validate_PropagatesRatingCurveValidation()
    {
        var analysis = CreateTestAnalysis();

        var (isValid, _) = analysis.Validate();

        Assert.IsTrue(isValid, "Validation should pass for valid rating curve.");
    }

    /// <summary>
    /// Validate fails when MinStage is NaN.
    /// </summary>
    [TestMethod]
    public void Validate_WithNaNMinStage_ReturnsInvalid()
    {
        var analysis = CreateTestAnalysis();
        analysis.UseDefaultStageBins = false;
        analysis.MinStage = double.NaN;

        var (isValid, messages) = analysis.Validate();

        Assert.IsFalse(isValid, "Validation should fail for NaN MinStage.");
        Assert.IsTrue(messages.Any(m => m.Contains("Minimum stage")),
            "Should have message about invalid MinStage.");
    }

    /// <summary>
    /// Validate fails when MaxStage is NaN.
    /// </summary>
    [TestMethod]
    public void Validate_WithNaNMaxStage_ReturnsInvalid()
    {
        var analysis = CreateTestAnalysis();
        analysis.UseDefaultStageBins = false;
        analysis.MaxStage = double.NaN;

        var (isValid, messages) = analysis.Validate();

        Assert.IsFalse(isValid, "Validation should fail for NaN MaxStage.");
        Assert.IsTrue(messages.Any(m => m.Contains("Maximum stage")),
            "Should have message about invalid MaxStage.");
    }

    /// <summary>
    /// Validate fails when MinStage >= MaxStage.
    /// </summary>
    [TestMethod]
    public void Validate_WithInvertedStageRange_ReturnsInvalid()
    {
        var analysis = CreateTestAnalysis();
        analysis.UseDefaultStageBins = false;
        analysis.MinStage = 10.0;
        analysis.MaxStage = 5.0;

        var (isValid, messages) = analysis.Validate();

        Assert.IsFalse(isValid, "Validation should fail when MinStage >= MaxStage.");
        Assert.IsTrue(messages.Any(m => m.Contains("less than")),
            "Should have message about stage range.");
    }

    /// <summary>
    /// Validate fails when StageBins is out of valid range.
    /// </summary>
    [TestMethod]
    [DataRow(5)]
    [DataRow(1001)]
    public void Validate_WithInvalidStageBins_ReturnsInvalid(int stageBins)
    {
        var analysis = CreateTestAnalysis();
        analysis.StageBins = stageBins;

        var (isValid, messages) = analysis.Validate();

        Assert.IsFalse(isValid, $"Validation should fail for StageBins = {stageBins}.");
        Assert.IsTrue(messages.Any(m => m.Contains("stage bins")),
            "Should have message about invalid StageBins.");
    }

    /// <summary>
    /// Validate succeeds with valid stage bins at the boundary values.
    /// </summary>
    [TestMethod]
    [DataRow(10)]
    [DataRow(1000)]
    public void Validate_WithValidStageBinsAtBoundary_ReturnsValid(int stageBins)
    {
        var analysis = CreateTestAnalysis();
        analysis.StageBins = stageBins;

        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid,
            $"Validation should pass for StageBins = {stageBins}. Messages: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Analysis validates correctly with various segment counts.
    /// </summary>
    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void Validate_WithVariousSegmentCounts_ReturnsValid(int segments)
    {
        var analysis = CreateTestAnalysis(segments);

        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid,
            $"Validation should pass for {segments} segment(s). Messages: {string.Join(", ", messages)}");
    }

    #endregion

    #region ClearResults Tests

    /// <summary>
    /// ClearResults resets all analysis results.
    /// </summary>
    [TestMethod]
    public void ClearResults_ResetsAllResults()
    {
        var analysis = CreateTestAnalysis();

        analysis.ClearResults();

        Assert.IsFalse(analysis.IsEstimated, "IsEstimated should be false after clearing.");
        Assert.IsNull(analysis.AnalysisResults, "AnalysisResults should be null after clearing.");
    }

    /// <summary>
    /// ClearResults also clears the underlying BayesianAnalysis results.
    /// </summary>
    [TestMethod]
    public void ClearResults_ClearsBayesianAnalysisResults()
    {
        var analysis = CreateTestAnalysis();

        analysis.ClearResults();

        Assert.IsFalse(analysis.BayesianAnalysis.IsEstimated,
            "BayesianAnalysis.IsEstimated should be false after clearing.");
    }

    #endregion

    #region Property Change Tests

    /// <summary>
    /// MinStage change raises PropertyChanged.
    /// </summary>
    [TestMethod]
    public void MinStage_Change_RaisesPropertyChanged()
    {
        var analysis = CreateTestAnalysis();
        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(RatingCurveAnalysis.MinStage))
                propertyChanged = true;
        };

        analysis.MinStage = 0.5;

        Assert.IsTrue(propertyChanged, "PropertyChanged should be raised for MinStage.");
    }

    /// <summary>
    /// MaxStage change raises PropertyChanged.
    /// </summary>
    [TestMethod]
    public void MaxStage_Change_RaisesPropertyChanged()
    {
        var analysis = CreateTestAnalysis();
        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(RatingCurveAnalysis.MaxStage))
                propertyChanged = true;
        };

        analysis.MaxStage = 20.0;

        Assert.IsTrue(propertyChanged, "PropertyChanged should be raised for MaxStage.");
    }

    /// <summary>
    /// StageBins change raises PropertyChanged.
    /// </summary>
    [TestMethod]
    public void StageBins_Change_RaisesPropertyChanged()
    {
        var analysis = CreateTestAnalysis();
        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(RatingCurveAnalysis.StageBins))
                propertyChanged = true;
        };

        analysis.StageBins = 50;

        Assert.IsTrue(propertyChanged, "PropertyChanged should be raised for StageBins.");
    }

    /// <summary>
    /// UseDefaultStageBins change raises PropertyChanged.
    /// </summary>
    [TestMethod]
    public void UseDefaultStageBins_Change_RaisesPropertyChanged()
    {
        var analysis = CreateTestAnalysis();
        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(RatingCurveAnalysis.UseDefaultStageBins))
                propertyChanged = true;
        };

        analysis.UseDefaultStageBins = false;

        Assert.IsTrue(propertyChanged, "PropertyChanged should be raised for UseDefaultStageBins.");
    }

    /// <summary>
    /// Setting UseDefaultStageBins to true resets stage bins to data-derived values.
    /// </summary>
    [TestMethod]
    public void UseDefaultStageBins_SetToTrue_ResetsFromData()
    {
        var analysis = CreateTestAnalysis();
        analysis.UseDefaultStageBins = false;
        analysis.MinStage = -100;
        analysis.MaxStage = 100;
        analysis.StageBins = 25;

        analysis.UseDefaultStageBins = true;

        Assert.AreEqual(100, analysis.StageBins, "StageBins should be reset to 100.");
        Assert.IsTrue(analysis.MinStage > -100, "MinStage should be reset from data.");
        Assert.IsTrue(analysis.MaxStage < 100, "MaxStage should be reset from data.");
    }

    /// <summary>
    /// Setting same MinStage value does not raise PropertyChanged.
    /// </summary>
    [TestMethod]
    public void MinStage_SetSameValue_DoesNotRaisePropertyChanged()
    {
        var analysis = CreateTestAnalysis();
        double originalMin = analysis.MinStage;
        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(RatingCurveAnalysis.MinStage))
                propertyChanged = true;
        };

        analysis.MinStage = originalMin;

        Assert.IsFalse(propertyChanged, "PropertyChanged should not be raised for same value.");
    }

    /// <summary>
    /// Setting same MaxStage value does not raise PropertyChanged.
    /// </summary>
    [TestMethod]
    public void MaxStage_SetSameValue_DoesNotRaisePropertyChanged()
    {
        var analysis = CreateTestAnalysis();
        double originalMax = analysis.MaxStage;
        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(RatingCurveAnalysis.MaxStage))
                propertyChanged = true;
        };

        analysis.MaxStage = originalMax;

        Assert.IsFalse(propertyChanged, "PropertyChanged should not be raised for same value.");
    }

    /// <summary>
    /// Setting same StageBins value does not raise PropertyChanged.
    /// </summary>
    [TestMethod]
    public void StageBins_SetSameValue_DoesNotRaisePropertyChanged()
    {
        var analysis = CreateTestAnalysis();
        int originalBins = analysis.StageBins;
        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(RatingCurveAnalysis.StageBins))
                propertyChanged = true;
        };

        analysis.StageBins = originalBins;

        Assert.IsFalse(propertyChanged, "PropertyChanged should not be raised for same value.");
    }

    #endregion

    #region Stage Bin Configuration Tests

    /// <summary>
    /// Default stage bins have expected initial values.
    /// </summary>
    [TestMethod]
    public void StageBins_DefaultInitialization_HasDefaultValues()
    {
        var analysis = CreateTestAnalysis();

        Assert.AreEqual(100, analysis.StageBins, "Default StageBins should be 100.");
        Assert.IsTrue(analysis.UseDefaultStageBins, "UseDefaultStageBins should be true by default.");
    }

    /// <summary>
    /// Stage bin properties can be modified.
    /// </summary>
    [TestMethod]
    public void StageBins_CanBeModified()
    {
        var analysis = CreateTestAnalysis();
        analysis.UseDefaultStageBins = false;

        analysis.MinStage = 1.0;
        analysis.MaxStage = 20.0;
        analysis.StageBins = 200;

        Assert.AreEqual(1.0, analysis.MinStage, 1e-10, "MinStage should be 1.0.");
        Assert.AreEqual(20.0, analysis.MaxStage, 1e-10, "MaxStage should be 20.0.");
        Assert.AreEqual(200, analysis.StageBins, "StageBins should be 200.");
    }

    /// <summary>
    /// Default stage bins are 10% wider than the observed data range.
    /// </summary>
    [TestMethod]
    public void DefaultStageBins_HasPaddingFromDataRange()
    {
        var (stageTS, dischargeTS) = MakeSingleSegmentData();
        double dataMin = stageTS.MinValue();
        double dataMax = stageTS.MaxValue();
        double range = dataMax - dataMin;

        var ratingCurve = new RMC.BestFit.Models.RatingCurve(stageTS, dischargeTS);
        var analysis = new RatingCurveAnalysis(ratingCurve);

        double expectedMin = dataMin - 0.1 * range;
        double expectedMax = dataMax + 0.1 * range;

        Assert.AreEqual(expectedMin, analysis.MinStage, 1e-6, "MinStage should have 10% padding.");
        Assert.AreEqual(expectedMax, analysis.MaxStage, 1e-6, "MaxStage should have 10% padding.");
    }

    #endregion

    #region BayesianAnalysis Configuration Tests

    /// <summary>
    /// BayesianAnalysis settings can be configured on the analysis.
    /// </summary>
    [TestMethod]
    public void BayesianAnalysis_CanBeConfigured()
    {
        var analysis = CreateTestAnalysis();

        analysis.BayesianAnalysis.Iterations = 10000;
        analysis.BayesianAnalysis.WarmupIterations = 2000;
        analysis.BayesianAnalysis.ThinningInterval = 10;
        analysis.BayesianAnalysis.CredibleIntervalWidth = 0.90;

        Assert.AreEqual(10000, analysis.BayesianAnalysis.Iterations);
        Assert.AreEqual(2000, analysis.BayesianAnalysis.WarmupIterations);
        Assert.AreEqual(10, analysis.BayesianAnalysis.ThinningInterval);
        Assert.AreEqual(0.90, analysis.BayesianAnalysis.CredibleIntervalWidth, 1e-10);
    }

    /// <summary>
    /// UseSimulationDefaults flag can be set on the BayesianAnalysis.
    /// </summary>
    [TestMethod]
    public void BayesianAnalysis_UseSimulationDefaults_CanBeSet()
    {
        var analysis = CreateTestAnalysis();

        analysis.BayesianAnalysis.UseSimulationDefaults = false;

        Assert.IsFalse(analysis.BayesianAnalysis.UseSimulationDefaults);
    }

    #endregion

    #region Rating Curve Model Property Propagation Tests

    /// <summary>
    /// Adding stage data with a wider range updates the default stage bins.
    /// </summary>
    [TestMethod]
    public void RatingCurveStageDataChange_UpdatesDefaultStageBins()
    {
        var analysis = CreateTestAnalysis();
        double originalMin = analysis.MinStage;
        double originalMax = analysis.MaxStage;

        var newDate = analysis.RatingCurve.StageData.Last().Index.AddDays(1);
        analysis.RatingCurve.StageData.Add(new SeriesOrdinate<DateTime, double>(newDate, 50.0));

        if (analysis.UseDefaultStageBins)
        {
            Assert.IsTrue(analysis.MaxStage > originalMax || analysis.MinStage != originalMin,
                "Stage bins should update when data changes.");
        }
    }

    /// <summary>
    /// Changing NumberOfSegments on the rating curve clears analysis results.
    /// </summary>
    [TestMethod]
    public void RatingCurveNumberOfSegmentsChange_ClearsResults()
    {
        var analysis = CreateTestAnalysis(1);

        analysis.RatingCurve.NumberOfSegments = 2;

        Assert.IsNull(analysis.AnalysisResults, "AnalysisResults should be null after segment change.");
        Assert.IsFalse(analysis.IsEstimated, "IsEstimated should be false after segment change.");
    }

    #endregion

    #region Jeffreys Prior Tests

    /// <summary>
    /// UseJeffreysRuleForScale on the rating curve can be configured through the analysis.
    /// </summary>
    [TestMethod]
    public void RatingCurve_UseJeffreysRuleForScale_CanBeConfigured()
    {
        var analysis = CreateTestAnalysis();

        analysis.RatingCurve.UseJeffreysRuleForScale = true;

        Assert.IsTrue(analysis.RatingCurve.UseJeffreysRuleForScale);
    }

    /// <summary>
    /// UseDefaultFlatPriors on the rating curve can be configured through the analysis.
    /// </summary>
    [TestMethod]
    public void RatingCurve_UseDefaultFlatPriors_CanBeConfigured()
    {
        var analysis = CreateTestAnalysis();

        analysis.RatingCurve.UseDefaultFlatPriors = false;

        Assert.IsFalse(analysis.RatingCurve.UseDefaultFlatPriors);
    }

    #endregion

    #region Event Tests (cancel-immediately path — no MCMC actually runs)

    /// <summary>
    /// AnalysisStarting event is raised when RunAsync is called.
    /// </summary>
    /// <remarks>
    /// The event handler cancels the run immediately — no MCMC chain executes.
    /// </remarks>
    [TestMethod]
    public async Task RunAsync_RaisesAnalysisStartingEvent()
    {
        var analysis = CreateTestAnalysis();
        analysis.BayesianAnalysis.Iterations = 100;
        analysis.BayesianAnalysis.WarmupIterations = 50;

        bool eventRaised = false;
        analysis.AnalysisStarting += (s, e) =>
        {
            eventRaised = true;
            e.Cancel = true; // Cancel immediately to avoid running MCMC
        };

        await analysis.RunAsync();

        Assert.IsTrue(eventRaised, "AnalysisStarting event should be raised.");
    }

    /// <summary>
    /// AnalysisCompleted event is raised after RunAsync completes (cancelled path).
    /// </summary>
    [TestMethod]
    public async Task RunAsync_RaisesAnalysisCompletedEvent()
    {
        var analysis = CreateTestAnalysis();

        bool eventRaised = false;
        analysis.AnalysisStarting += (s, e) => e.Cancel = true; // Cancel immediately
        analysis.AnalysisCompleted += (s, e) =>
        {
            eventRaised = true;
            Assert.IsTrue(e.Cancelled, "Cancelled should be true when canceled.");
        };

        await analysis.RunAsync();

        Assert.IsTrue(eventRaised, "AnalysisCompleted event should be raised.");
    }

    /// <summary>
    /// Cancellation during analysis sets the Cancelled flag.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_WhenCanceled_SetsCancelledFlag()
    {
        var analysis = CreateTestAnalysis();
        bool wasCanceled = false;

        analysis.AnalysisStarting += (s, e) => e.Cancel = true;
        analysis.AnalysisCompleted += (s, e) =>
        {
            wasCanceled = e.Cancelled;
        };

        await analysis.RunAsync();

        Assert.IsTrue(wasCanceled, "Cancelled should be true.");
        Assert.IsFalse(analysis.IsEstimated, "IsEstimated should be false when canceled.");
    }

    #endregion

    #region CancelAnalysis Tests

    /// <summary>
    /// CancelAnalysis can be called when nothing is running without throwing.
    /// </summary>
    [TestMethod]
    public void CancelAnalysis_WhenNotRunning_DoesNotThrow()
    {
        var analysis = CreateTestAnalysis();

        analysis.CancelAnalysis();
    }

    /// <summary>
    /// CancelAnalysis cancels the underlying BayesianAnalysis without throwing.
    /// </summary>
    [TestMethod]
    public void CancelAnalysis_CancelsBayesianAnalysis()
    {
        var analysis = CreateTestAnalysis();

        analysis.CancelAnalysis();
        // Confirms the method does not throw; we cannot verify
        // internal cancellation state without running an MCMC chain.
    }

    #endregion

    #region RunAsync Invalid Configuration Tests

    /// <summary>
    /// RunAsync throws InvalidOperationException for invalid configuration.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_WithInvalidConfiguration_ThrowsInvalidOperationException()
    {
        var analysis = CreateTestAnalysis();
        analysis.UseDefaultStageBins = false;
        analysis.MinStage = 100;  // Invalid: greater than MaxStage
        analysis.MaxStage = 50;

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await analysis.RunAsync(),
            "RunAsync should throw for invalid configuration.");
    }

    #endregion
}
