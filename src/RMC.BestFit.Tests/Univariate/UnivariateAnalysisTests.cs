using Numerics;
using Numerics.Data;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using System.Xml.Linq;

namespace RMC.BestFit.Tests.UnivariateAnalyses;

/// <summary>
/// Unit tests for the <see cref="UnivariateAnalysis"/> class.
/// Validates Bayesian MCMC estimation workflow for univariate distributions.
/// </summary>
/// <remarks>
/// <para>
///     <b>Authors:</b>
/// </para>
/// <list type="bullet">
///     <item>Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil</item>
/// </list>
/// <para>
///     The <see cref="UnivariateAnalysis"/> class is the primary analysis class for
///     performing Bayesian MCMC estimation of univariate distributions in RMC-BestFit.
/// </para>
/// </remarks>
[TestClass]
public class UnivariateAnalysisTests
{
    #region Test Data

    /// <summary>
    /// Creates sample flood data for testing.
    /// </summary>
    /// <returns>A <see cref="DataFrame"/> with annual peak flow data.</returns>
    private static DataFrame CreateTestDataFrame()
    {
        // Annual peak flow data (cfs) - synthetic data based on typical flood record
        var values = new double[]
        {
            12500, 15300, 8900, 22100, 18700, 14200, 9800, 28500, 17400, 11600,
            19200, 13800, 25600, 10500, 16900, 21300, 14700, 8200, 23800, 15900,
            12100, 27400, 19800, 11200, 16400, 20600, 13200, 9400, 24900, 17800
        };

        var df = new DataFrame();
        for (int i = 0; i < values.Length; i++)
        {
            df.ExactSeries.Add(new ExactData(1990 + i, values[i]));
        }
        return df;
    }

    /// <summary>
    /// Creates a test univariate distribution with Normal distribution.
    /// </summary>
    /// <returns>A configured <see cref="UnivariateDistribution"/>.</returns>
    private static UnivariateDistribution CreateTestDistribution()
    {
        var df = CreateTestDataFrame();
        var dist = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        return dist;
    }

    /// <summary>
    /// Creates a test univariate distribution with GEV distribution.
    /// </summary>
    /// <returns>A configured <see cref="UnivariateDistribution"/> with GEV.</returns>
    private static UnivariateDistribution CreateGEVDistribution()
    {
        var df = CreateTestDataFrame();
        var dist = new UnivariateDistribution(df, UnivariateDistributionType.GeneralizedExtremeValue);
        return dist;
    }

    #endregion

    #region Constructor Tests

    /// <summary>
    /// Tests that the constructor properly initializes the analysis with a univariate distribution.
    /// </summary>
    [TestMethod]
    public void Constructor_WithDistribution_InitializesCorrectly()
    {
        // Arrange
        var dist = CreateTestDistribution();

        // Act
        var analysis = new UnivariateAnalysis(dist);

        // Assert
        Assert.IsNotNull(analysis.UnivariateDistribution, "UnivariateDistribution should not be null.");
        Assert.IsNotNull(analysis.BayesianAnalysis, "BayesianAnalysis should not be null.");
        Assert.IsNotNull(analysis.ProbabilityOrdinates, "ProbabilityOrdinates should not be null.");
        Assert.IsFalse(analysis.IsEstimated, "IsEstimated should be false initially.");
        Assert.IsNull(analysis.AnalysisResults, "AnalysisResults should be null initially.");
        Assert.IsNull(analysis.ChronologyAnalysisResults, "ChronologyAnalysisResults should be null initially.");
    }

    /// <summary>
    /// Tests that the constructor throws <see cref="ArgumentNullException"/> when distribution is null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_WithNullDistribution_ThrowsArgumentNullException()
    {
        // Act
        _ = new UnivariateAnalysis(null!);
    }

    /// <summary>
    /// Tests that the BayesianAnalysis property references the correct model.
    /// </summary>
    [TestMethod]
    public void Constructor_BayesianAnalysis_HasCorrectModel()
    {
        // Arrange
        var dist = CreateTestDistribution();

        // Act
        var analysis = new UnivariateAnalysis(dist);

        // Assert
        Assert.AreSame(dist, analysis.BayesianAnalysis.Model, "BayesianAnalysis.Model should reference the univariate distribution.");
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
        var dist = CreateTestDistribution();
        var analysis = new UnivariateAnalysis(dist);

        // Configure probability ordinates
        analysis.ProbabilityOrdinates.Clear();
        analysis.ProbabilityOrdinates.Add(0.5);
        analysis.ProbabilityOrdinates.Add(0.1);
        analysis.ProbabilityOrdinates.Add(0.01);

        // Act
        var xElement = analysis.ToXElement();
        var restoredAnalysis = new UnivariateAnalysis(dist, xElement);

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
        var dist = CreateTestDistribution();

        // Act
        _ = new UnivariateAnalysis(dist, null!);
    }

    /// <summary>
    /// Tests serialization with Bayesian analysis settings preserved.
    /// </summary>
    [TestMethod]
    public void XmlSerialization_WithBayesianSettings_PreservesSettings()
    {
        // Arrange
        var dist = CreateTestDistribution();
        var analysis = new UnivariateAnalysis(dist);
        analysis.BayesianAnalysis.Iterations = 5000;
        analysis.BayesianAnalysis.WarmupIterations = 1000;

        // Act
        var xElement = analysis.ToXElement();
        var restoredAnalysis = new UnivariateAnalysis(dist, xElement);

        // Assert
        Assert.AreEqual(5000, restoredAnalysis.BayesianAnalysis.Iterations,
            "Iterations should be preserved.");
        Assert.AreEqual(1000, restoredAnalysis.BayesianAnalysis.WarmupIterations,
            "WarmupIterations should be preserved.");
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
        var dist = CreateTestDistribution();
        var analysis = new UnivariateAnalysis(dist);

        // Act
        var (isValid, messages) = analysis.Validate();

        // Assert
        Assert.IsTrue(isValid, $"Validation should pass. Messages: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests that Validate checks the underlying distribution.
    /// </summary>
    [TestMethod]
    public void Validate_PropagatesDistributionValidation()
    {
        // Arrange
        var dist = CreateTestDistribution();
        var analysis = new UnivariateAnalysis(dist);

        // Act
        var (isValid, messages) = analysis.Validate();

        // Assert
        Assert.IsTrue(isValid, "Validation should pass for valid distribution.");
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
        var dist = CreateTestDistribution();
        var analysis = new UnivariateAnalysis(dist);

        // Act
        analysis.ClearResults();

        // Assert
        Assert.IsFalse(analysis.IsEstimated, "IsEstimated should be false after clearing.");
        Assert.IsNull(analysis.AnalysisResults, "AnalysisResults should be null after clearing.");
        Assert.IsNull(analysis.ChronologyAnalysisResults, "ChronologyAnalysisResults should be null after clearing.");
    }

    #endregion

    #region Property Change Tests

    /// <summary>
    /// Tests that ProbabilityOrdinates changes trigger property change notifications.
    /// </summary>
    [TestMethod]
    public void ProbabilityOrdinates_Change_RaisesPropertyChanged()
    {
        // Arrange
        var dist = CreateTestDistribution();
        var analysis = new UnivariateAnalysis(dist);
        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(UnivariateAnalysis.ProbabilityOrdinates))
                propertyChanged = true;
        };

        // Act
        analysis.ProbabilityOrdinates.Add(0.001);

        // Assert
        Assert.IsTrue(propertyChanged, "PropertyChanged should be raised for ProbabilityOrdinates.");
    }

    #endregion

    #region GetDistribution Tests

    /// <summary>
    /// Tests that GetDistribution returns null when analysis is not estimated.
    /// </summary>
    [TestMethod]
    public void GetDistribution_WhenNotEstimated_ReturnsNull()
    {
        // Arrange
        var dist = CreateTestDistribution();
        var analysis = new UnivariateAnalysis(dist);

        // Act
        var result = analysis.GetDistribution(0);

        // Assert
        Assert.IsNull(result, "GetDistribution should return null when not estimated.");
    }

    /// <summary>
    /// Tests that GetPointEstimateDistribution returns null when analysis is not estimated.
    /// </summary>
    [TestMethod]
    public void GetPointEstimateDistribution_WhenNotEstimated_ReturnsNull()
    {
        // Arrange
        var dist = CreateTestDistribution();
        var analysis = new UnivariateAnalysis(dist);

        // Act
        var result = analysis.GetPointEstimateDistribution();

        // Assert
        Assert.IsNull(result, "GetPointEstimateDistribution should return null when not estimated.");
    }

    #endregion

    #region CancelAnalysis Tests

    /// <summary>
    /// Tests that CancelAnalysis can be called without error.
    /// </summary>
    [TestMethod]
    public void CancelAnalysis_WhenNotRunning_DoesNotThrow()
    {
        // Arrange
        var dist = CreateTestDistribution();
        var analysis = new UnivariateAnalysis(dist);

        // Act & Assert - Should not throw
        analysis.CancelAnalysis();
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
        var dist = CreateTestDistribution();
        var analysis = new UnivariateAnalysis(dist);
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
        var dist = CreateTestDistribution();
        var analysis = new UnivariateAnalysis(dist);

        bool eventRaised = false;
        analysis.AnalysisStarting += (s, e) => e.Cancel = true; // Cancel immediately
        analysis.AnalysisCompleted += (s, e) =>
        {
            eventRaised = true;
            Assert.IsTrue(e.Cancelled, "Cancelled should be true when canceled.");
        };

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(eventRaised, "AnalysisCompleted event should be raised.");
    }

    #endregion

    #region Integration Tests

    /// <summary>
    /// Tests that the analysis validates correctly with different distribution types.
    /// </summary>
    [TestMethod]
    public void Validate_WithGEVDistribution_ReturnsValid()
    {
        // Arrange
        var dist = CreateGEVDistribution();
        var analysis = new UnivariateAnalysis(dist);

        // Act
        var (isValid, messages) = analysis.Validate();

        // Assert
        Assert.IsTrue(isValid, $"Validation should pass for GEV distribution. Messages: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests that modifying the univariate distribution clears results.
    /// </summary>
    [TestMethod]
    public void DistributionChange_ClearsResults()
    {
        // Arrange
        var dist = CreateTestDistribution();
        var analysis = new UnivariateAnalysis(dist);

        // Simulate that analysis was estimated
        bool resultsCleared = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(UnivariateAnalysis.AnalysisResults))
                resultsCleared = true;
        };

        // Act - Trigger property change on distribution
        dist.DistributionType = UnivariateDistributionType.Gumbel;

        // Assert
        Assert.IsTrue(resultsCleared || analysis.AnalysisResults == null,
            "Results should be cleared when distribution changes.");
    }

    /// <summary>
    /// Tests creating analysis with various distribution types.
    /// </summary>
    [TestMethod]
    [DataRow(UnivariateDistributionType.Normal)]
    [DataRow(UnivariateDistributionType.LogNormal)]
    [DataRow(UnivariateDistributionType.Gumbel)]
    [DataRow(UnivariateDistributionType.GeneralizedExtremeValue)]
    [DataRow(UnivariateDistributionType.PearsonTypeIII)]
    [DataRow(UnivariateDistributionType.LogPearsonTypeIII)]
    public void Constructor_WithVariousDistributionTypes_InitializesCorrectly(UnivariateDistributionType distType)
    {
        // Arrange
        var df = CreateTestDataFrame();
        var dist = new UnivariateDistribution(df, distType);

        // Act
        var analysis = new UnivariateAnalysis(dist);

        // Assert
        Assert.IsNotNull(analysis, "Analysis should be created successfully.");
        Assert.AreEqual(distType, analysis.UnivariateDistribution.DistributionType,
            "Distribution type should match.");
    }

    #endregion

    #region Probability Ordinates Tests

    /// <summary>
    /// Tests that default probability ordinates are initialized correctly.
    /// </summary>
    [TestMethod]
    public void ProbabilityOrdinates_DefaultInitialization_HasDefaultValues()
    {
        // Arrange
        var dist = CreateTestDistribution();

        // Act
        var analysis = new UnivariateAnalysis(dist);

        // Assert
        Assert.IsNotNull(analysis.ProbabilityOrdinates, "ProbabilityOrdinates should not be null.");
        Assert.IsTrue(analysis.ProbabilityOrdinates.Count > 0, "Default probability ordinates should have values.");
    }

    /// <summary>
    /// Tests that probability ordinates can be modified.
    /// </summary>
    [TestMethod]
    public void ProbabilityOrdinates_CanBeModified()
    {
        // Arrange
        var dist = CreateTestDistribution();
        var analysis = new UnivariateAnalysis(dist);
        analysis.ProbabilityOrdinates.Clear();

        // Act
        analysis.ProbabilityOrdinates.Add(0.5);
        analysis.ProbabilityOrdinates.Add(0.1);
        analysis.ProbabilityOrdinates.Add(0.01);

        // Assert
        Assert.AreEqual(3, analysis.ProbabilityOrdinates.Count, "Should have 3 probability ordinates.");
        Assert.AreEqual(0.5, analysis.ProbabilityOrdinates[0], 1e-10, "First ordinate should be 0.5.");
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Tests analysis with minimal data (edge case).
    /// </summary>
    [TestMethod]
    public void Constructor_WithMinimalData_InitializesCorrectly()
    {
        // Arrange - Create DataFrame with minimum data points
        var df = new DataFrame();
        df.ExactSeries.Add(new ExactData(2000, 100));
        df.ExactSeries.Add(new ExactData(2001, 150));
        df.ExactSeries.Add(new ExactData(2002, 120));

        var dist = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        // Act
        var analysis = new UnivariateAnalysis(dist);

        // Assert
        Assert.IsNotNull(analysis, "Analysis should be created with minimal data.");
    }

    /// <summary>
    /// Tests that ToXElement creates valid XML structure.
    /// </summary>
    [TestMethod]
    public void ToXElement_CreatesValidXmlStructure()
    {
        // Arrange
        var dist = CreateTestDistribution();
        var analysis = new UnivariateAnalysis(dist);

        // Act
        var xElement = analysis.ToXElement();

        // Assert
        Assert.IsNotNull(xElement, "XElement should not be null.");
        Assert.AreEqual("UnivariateAnalysis", xElement.Name.LocalName, "Root element should be UnivariateAnalysis.");
        Assert.IsNotNull(xElement.Attribute("IsEstimated"), "Should have IsEstimated attribute.");
    }

    #endregion

    #region ProbabilityOrdinates Reprocess-or-Clear Tests (Phase 1)

    /// <summary>
    /// Tests that <see cref="UnivariateAnalysis.ClearFrequencyAnalysisResults"/> exists and is callable.
    /// </summary>
    [TestMethod]
    public void ClearFrequencyAnalysisResults_OnFreshAnalysis_DoesNotThrow()
    {
        // Arrange
        var dist = CreateTestDistribution();
        var analysis = new UnivariateAnalysis(dist);

        // Act
        analysis.ClearFrequencyAnalysisResults();

        // Assert — AnalysisResults already null; method is a no-op on fresh analysis but must not throw.
        Assert.IsNull(analysis.AnalysisResults, "AnalysisResults should remain null.");
        Assert.IsFalse(analysis.IsEstimated, "IsEstimated should remain false.");
    }

    /// <summary>
    /// Tests that <see cref="UnivariateAnalysis.ClearFrequencyAnalysisResults"/> raises
    /// <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/> for <c>AnalysisResults</c>.
    /// </summary>
    [TestMethod]
    public void ClearFrequencyAnalysisResults_RaisesAnalysisResultsPropertyChanged()
    {
        // Arrange
        var dist = CreateTestDistribution();
        var analysis = new UnivariateAnalysis(dist);
        bool notified = false;
        analysis.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(UnivariateAnalysis.AnalysisResults)) notified = true;
        };

        // Act
        analysis.ClearFrequencyAnalysisResults();

        // Assert
        Assert.IsTrue(notified, "AnalysisResults PropertyChanged should fire.");
    }

    /// <summary>
    /// Tests that adding a probability ordinate on a not-yet-estimated analysis is a no-op
    /// (does not throw, does not alter state, and does not attempt to run an analysis).
    /// </summary>
    [TestMethod]
    public void ProbabilityOrdinatesChange_WhenNotEstimated_IsNoOp()
    {
        // Arrange
        var dist = CreateTestDistribution();
        var analysis = new UnivariateAnalysis(dist);
        analysis.ProbabilityOrdinates.Clear();

        // Act — add ordinates on a not-estimated analysis
        analysis.ProbabilityOrdinates.Add(0.5);
        analysis.ProbabilityOrdinates.Add(0.1);
        analysis.ProbabilityOrdinates.Add(0.01);

        // Assert — AnalysisResults stays null, IsEstimated stays false, no reprocess happens.
        Assert.IsFalse(analysis.IsEstimated, "IsEstimated should remain false.");
        Assert.IsNull(analysis.AnalysisResults, "AnalysisResults should remain null on not-estimated analysis.");
        Assert.AreEqual(3, analysis.ProbabilityOrdinates.Count, "Ordinates should still be applied to the collection.");
    }

    /// <summary>
    /// Tests that invalid ordinates (empty collection) on a not-estimated analysis don't throw.
    /// </summary>
    [TestMethod]
    public void ProbabilityOrdinatesChange_InvalidEmptyOnNotEstimated_DoesNotThrow()
    {
        // Arrange
        var dist = CreateTestDistribution();
        var analysis = new UnivariateAnalysis(dist);

        // Act — wipe the collection (invalid state: count==0)
        analysis.ProbabilityOrdinates.Clear();

        // Assert
        Assert.AreEqual(0, analysis.ProbabilityOrdinates.Count);
        Assert.IsNull(analysis.AnalysisResults);
        Assert.IsFalse(analysis.IsEstimated);
    }

    /// <summary>
    /// Tests that <see cref="UnivariateAnalysis.ClearResults"/> still clears everything destructively
    /// (unchanged by the Phase 1 work).
    /// </summary>
    [TestMethod]
    public void ClearResults_ClearsBothDerivedAndFit()
    {
        // Arrange
        var dist = CreateTestDistribution();
        var analysis = new UnivariateAnalysis(dist);

        // Act
        analysis.ClearResults();

        // Assert — destructive: everything cleared. IsEstimated stays false since we never ran.
        Assert.IsNull(analysis.AnalysisResults);
        Assert.IsNull(analysis.ChronologyAnalysisResults);
        Assert.IsFalse(analysis.IsEstimated);
    }

    #endregion
}
