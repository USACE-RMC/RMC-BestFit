using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using System.Reflection;
using System.Xml.Linq;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Programmatic unit tests for the <c>CompetingRiskAnalysis</c> class.
/// </summary>
/// <remarks>
/// Configuration, validation, and serialization tests live here.
/// MCMC parameter-recovery and estimation tests live in <c>RMC.BestFit.Verification</c>.
/// </remarks>
[TestClass]
public class CompetingRiskAnalysisTests
{
    #region Inline test fixtures

    private const int FixtureSize = 30;

    /// <summary>
    /// Inline Gumbel(15000, 4000) flood-like fixture. Generated from a fixed RNG seed
    /// so this file doesn't depend on the Verification project's <c>TestData</c>.
    /// </summary>
    private static readonly double[] InlinePeakData = new Gumbel(15000.0, 4000.0)
        .GenerateRandomValues(FixtureSize, 12345);

    /// <summary>
    /// Creates test Data Frame.
    /// </summary>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static BestFitDataFrame CreateTestDataFrame()
    {
        var df = new BestFitDataFrame();
        for (int i = 0; i < InlinePeakData.Length; i++)
        {
            df.ExactSeries.Add(new ExactData(1990 + i, InlinePeakData[i]));
        }
        return df;
    }

    /// <summary>
    /// Creates test Model.
    /// </summary>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static CompetingRisksModel CreateTestModel()
    {
        var df = CreateTestDataFrame();
        var distributionTypes = new List<UnivariateDistributionType>
        {
            UnivariateDistributionType.Gumbel,
            UnivariateDistributionType.Gumbel
        };
        return new CompetingRisksModel(df, distributionTypes);
    }

    /// <summary>
    /// Creates gEV Model.
    /// </summary>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static CompetingRisksModel CreateGEVModel()
    {
        var df = CreateTestDataFrame();
        var distributionTypes = new List<UnivariateDistributionType>
        {
            UnivariateDistributionType.GeneralizedExtremeValue,
            UnivariateDistributionType.GeneralizedExtremeValue
        };
        return new CompetingRisksModel(df, distributionTypes);
    }

    /// <summary>
    /// Creates an estimated competing-risks analysis with stub uncertainty results.
    /// </summary>
    /// <param name="model">The competing-risks model to attach to the analysis.</param>
    /// <returns>An analysis whose stale estimated state can be invalidated without running MCMC.</returns>
    /// <remarks>
    /// The helper uses the XML constructor and reflection so tests stay fast and avoid computational verification.
    /// </remarks>
    private static CompetingRiskAnalysis CreateEstimatedAnalysis(CompetingRisksModel model)
    {
        var xElement = new XElement("CompetingRiskAnalysis", new XAttribute("IsEstimated", true));
        var analysis = new CompetingRiskAnalysis(model, xElement);
        var resultsProperty = typeof(CompetingRiskAnalysis).GetProperty(nameof(CompetingRiskAnalysis.AnalysisResults),
            BindingFlags.Instance | BindingFlags.Public)!;
        resultsProperty.GetSetMethod(true)!.Invoke(analysis, new object[] { new UncertaintyAnalysisResults() });
        return analysis;
    }

    #endregion

    #region Constructor Tests

    /// <summary>Verifies that constructor with model initializes correctly.</summary>
    [TestMethod]
    public void Constructor_WithModel_InitializesCorrectly()
    {
        var model = CreateTestModel();

        var analysis = new CompetingRiskAnalysis(model);

        Assert.IsNotNull(analysis.CompetingRisksDistribution, "CompetingRisksModel should not be null.");
        Assert.IsNotNull(analysis.BayesianAnalysis, "BayesianAnalysis should not be null.");
        Assert.IsNotNull(analysis.ProbabilityOrdinates, "ProbabilityOrdinates should not be null.");
        Assert.IsFalse(analysis.IsEstimated, "IsEstimated should be false initially.");
        Assert.IsNull(analysis.AnalysisResults, "AnalysisResults should be null initially.");
    }

    /// <summary>Verifies that constructor throws when with null model.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_WithNullModel_ThrowsArgumentNullException()
    {
        _ = new CompetingRiskAnalysis(null!);
    }

    /// <summary>Verifies that constructor bayesian analysis has correct model.</summary>
    [TestMethod]
    public void Constructor_BayesianAnalysis_HasCorrectModel()
    {
        var model = CreateTestModel();

        var analysis = new CompetingRiskAnalysis(model);

        Assert.AreSame(model, analysis.BayesianAnalysis.Model,
            "BayesianAnalysis.Model should reference the competing risks model.");
    }

    #endregion

    #region XML Serialization Tests

    /// <summary>Verifies that xml serialization preserves configuration for round trip.</summary>
    [TestMethod]
    public void XmlSerialization_RoundTrip_PreservesConfiguration()
    {
        var model = CreateTestModel();
        var analysis = new CompetingRiskAnalysis(model);
        analysis.ProbabilityOrdinates.Clear();
        analysis.ProbabilityOrdinates.Add(0.5);
        analysis.ProbabilityOrdinates.Add(0.1);
        analysis.ProbabilityOrdinates.Add(0.01);

        var xElement = analysis.ToXElement();
        var restoredAnalysis = new CompetingRiskAnalysis(model, xElement);

        Assert.IsNotNull(restoredAnalysis, "Restored analysis should not be null.");
        Assert.AreEqual(analysis.ProbabilityOrdinates.Count, restoredAnalysis.ProbabilityOrdinates.Count,
            "Probability ordinates count should be preserved.");
    }

    /// <summary>Verifies that constructor throws when with null X element.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_WithNullXElement_ThrowsArgumentNullException()
    {
        var model = CreateTestModel();
        _ = new CompetingRiskAnalysis(model, null!);
    }

    /// <summary>Verifies that xml serialization preserves settings for with bayesian settings.</summary>
    [TestMethod]
    public void XmlSerialization_WithBayesianSettings_PreservesSettings()
    {
        var model = CreateTestModel();
        var analysis = new CompetingRiskAnalysis(model);
        analysis.BayesianAnalysis.Iterations = 10000;
        analysis.BayesianAnalysis.WarmupIterations = 3000;

        var xElement = analysis.ToXElement();
        var restoredAnalysis = new CompetingRiskAnalysis(model, xElement);

        Assert.AreEqual(10000, restoredAnalysis.BayesianAnalysis.Iterations,
            "Iterations should be preserved.");
        Assert.AreEqual(3000, restoredAnalysis.BayesianAnalysis.WarmupIterations,
            "WarmupIterations should be preserved.");
    }

    #endregion

    #region Validation Tests

    /// <summary>Verifies that validate returns valid when with valid configuration.</summary>
    [TestMethod]
    public void Validate_WithValidConfiguration_ReturnsValid()
    {
        var model = CreateTestModel();
        var analysis = new CompetingRiskAnalysis(model);

        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Validation should pass. Messages: {string.Join(", ", messages)}");
    }

    /// <summary>Verifies that validate returns valid when with GEV components.</summary>
    [TestMethod]
    public void Validate_WithGEVComponents_ReturnsValid()
    {
        var model = CreateGEVModel();
        var analysis = new CompetingRiskAnalysis(model);

        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Validation should pass for GEV. Messages: {string.Join(", ", messages)}");
    }

    #endregion

    #region ClearResults Tests

    /// <summary>Verifies that clear results resets all results.</summary>
    [TestMethod]
    public void ClearResults_ResetsAllResults()
    {
        var model = CreateTestModel();
        var analysis = new CompetingRiskAnalysis(model);

        analysis.ClearResults();

        Assert.IsFalse(analysis.IsEstimated, "IsEstimated should be false after clearing.");
        Assert.IsNull(analysis.AnalysisResults, "AnalysisResults should be null after clearing.");
    }

    #endregion

    #region Property Change Tests

    /// <summary>Verifies that probability ordinates change raises property changed.</summary>
    [TestMethod]
    public void ProbabilityOrdinates_Change_RaisesPropertyChanged()
    {
        var model = CreateTestModel();
        var analysis = new CompetingRiskAnalysis(model);
        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(CompetingRiskAnalysis.ProbabilityOrdinates))
                propertyChanged = true;
        };

        analysis.ProbabilityOrdinates.Add(0.001);

        Assert.IsTrue(propertyChanged, "PropertyChanged should be raised for ProbabilityOrdinates.");
    }

    /// <summary>Verifies that enabling quantile priors clears stale analysis results.</summary>
    [TestMethod]
    public void EnableQuantilePriors_ClearsResults()
    {
        var model = CreateTestModel();
        var analysis = CreateEstimatedAnalysis(model);
        bool analysisResultsChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(CompetingRiskAnalysis.AnalysisResults))
                analysisResultsChanged = true;
        };

        model.EnableQuantilePriors = true;

        Assert.IsTrue(analysisResultsChanged, "AnalysisResults should be raised when quantile priors are enabled.");
        Assert.IsNull(analysis.AnalysisResults, "Quantile prior changes should clear stale AnalysisResults.");
        Assert.IsFalse(analysis.IsEstimated, "Quantile prior changes should invalidate the estimated state.");
        Assert.IsTrue(model.QuantilePriors.Count > 0, "Enabling quantile priors should create default prior rows.");
    }

    /// <summary>Verifies that editing an existing quantile prior clears stale analysis results.</summary>
    [TestMethod]
    public void QuantilePriorEdit_ClearsResults()
    {
        var model = CreateTestModel();
        model.EnableQuantilePriors = true;
        var analysis = CreateEstimatedAnalysis(model);
        bool analysisResultsChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(CompetingRiskAnalysis.AnalysisResults))
                analysisResultsChanged = true;
        };

        model.QuantilePriors[0].Alpha = 0.02;

        Assert.IsTrue(analysisResultsChanged, "AnalysisResults should be raised when a quantile prior is edited.");
        Assert.IsNull(analysis.AnalysisResults, "Quantile prior edits should clear stale AnalysisResults.");
        Assert.IsFalse(analysis.IsEstimated, "Quantile prior edits should invalidate the estimated state.");
    }

    /// <summary>Verifies that prior-related option changes clear stale analysis results.</summary>
    [TestMethod]
    public void PriorOptionChange_ClearsResults()
    {
        var model = CreateTestModel();
        var analysis = CreateEstimatedAnalysis(model);
        bool analysisResultsChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(CompetingRiskAnalysis.AnalysisResults))
                analysisResultsChanged = true;
        };

        model.UseJeffreysRuleForScale = !model.UseJeffreysRuleForScale;

        Assert.IsTrue(analysisResultsChanged, "AnalysisResults should be raised when prior options change.");
        Assert.IsNull(analysis.AnalysisResults, "Prior option changes should clear stale AnalysisResults.");
        Assert.IsFalse(analysis.IsEstimated, "Prior option changes should invalidate the estimated state.");
    }

    #endregion

    #region GetDistribution Tests

    /// <summary>Verifies that get distribution returns null when when not estimated.</summary>
    [TestMethod]
    public void GetDistribution_WhenNotEstimated_ReturnsNull()
    {
        var model = CreateTestModel();
        var analysis = new CompetingRiskAnalysis(model);

        var result = analysis.GetDistribution(0);

        Assert.IsNull(result, "GetDistribution should return null when not estimated.");
    }

    /// <summary>Verifies that get point estimate distribution returns null when when not estimated.</summary>
    [TestMethod]
    public void GetPointEstimateDistribution_WhenNotEstimated_ReturnsNull()
    {
        var model = CreateTestModel();
        var analysis = new CompetingRiskAnalysis(model);

        var result = analysis.GetPointEstimateDistribution();

        Assert.IsNull(result, "GetPointEstimateDistribution should return null when not estimated.");
    }

    #endregion

    #region CancelAnalysis Tests

    /// <summary>Verifies that cancel analysis does not throw for when not running.</summary>
    [TestMethod]
    public void CancelAnalysis_WhenNotRunning_DoesNotThrow()
    {
        var model = CreateTestModel();
        var analysis = new CompetingRiskAnalysis(model);

        analysis.CancelAnalysis();
    }

    #endregion

    #region Event Tests

    /// <summary>Verifies that run async raises analysis starting event.</summary>
    [TestMethod]
    public async Task RunAsync_RaisesAnalysisStartingEvent()
    {
        var model = CreateTestModel();
        var analysis = new CompetingRiskAnalysis(model);
        analysis.BayesianAnalysis.Iterations = 100;

        bool eventRaised = false;
        analysis.AnalysisStarting += (s, e) =>
        {
            eventRaised = true;
            e.Cancel = true; // Cancel immediately to keep test fast.
        };

        await analysis.RunAsync();

        Assert.IsTrue(eventRaised, "AnalysisStarting event should be raised.");
    }

    /// <summary>Verifies that run async raises analysis completed event.</summary>
    [TestMethod]
    public async Task RunAsync_RaisesAnalysisCompletedEvent()
    {
        var model = CreateTestModel();
        var analysis = new CompetingRiskAnalysis(model);

        bool eventRaised = false;
        analysis.AnalysisStarting += (s, e) => e.Cancel = true;
        analysis.AnalysisCompleted += (s, e) =>
        {
            eventRaised = true;
            Assert.IsTrue(e.Cancelled, "Cancelled should be true when canceled.");
        };

        await analysis.RunAsync();

        Assert.IsTrue(eventRaised, "AnalysisCompleted event should be raised.");
    }

    #endregion

    #region Integration Tests

    /// <summary>Verifies that constructor with various component types initializes correctly.</summary>
    [TestMethod]
    [DataRow(UnivariateDistributionType.Normal)]
    [DataRow(UnivariateDistributionType.LogNormal)]
    [DataRow(UnivariateDistributionType.Gumbel)]
    [DataRow(UnivariateDistributionType.GeneralizedExtremeValue)]
    public void Constructor_WithVariousComponentTypes_InitializesCorrectly(UnivariateDistributionType distType)
    {
        var df = CreateTestDataFrame();
        var distributionTypes = new List<UnivariateDistributionType> { distType, distType };
        var model = new CompetingRisksModel(df, distributionTypes);

        var analysis = new CompetingRiskAnalysis(model);

        Assert.IsNotNull(analysis, "Analysis should be created successfully.");
        Assert.AreEqual(distType, analysis.CompetingRisksDistribution.CompetingRisks!.Distributions[0].Type,
            "Component distribution type should match.");
    }

    /// <summary>Verifies that constructor with various component counts initializes correctly.</summary>
    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    public void Constructor_WithVariousComponentCounts_InitializesCorrectly(int componentCount)
    {
        var df = CreateTestDataFrame();
        var distributionTypes = Enumerable.Repeat(UnivariateDistributionType.Gumbel, componentCount).ToList();
        var model = new CompetingRisksModel(df, distributionTypes);

        var analysis = new CompetingRiskAnalysis(model);

        Assert.IsNotNull(analysis, "Analysis should be created successfully.");
        Assert.AreEqual(componentCount, analysis.CompetingRisksDistribution.CompetingRisks!.Distributions.Count,
            "Number of components should match.");
    }

    #endregion

    #region Probability Ordinates Tests

    /// <summary>Verifies that probability ordinates default initialization has default values.</summary>
    [TestMethod]
    public void ProbabilityOrdinates_DefaultInitialization_HasDefaultValues()
    {
        var model = CreateTestModel();

        var analysis = new CompetingRiskAnalysis(model);

        Assert.IsNotNull(analysis.ProbabilityOrdinates, "ProbabilityOrdinates should not be null.");
        Assert.IsTrue(analysis.ProbabilityOrdinates.Count > 0, "Default probability ordinates should have values.");
    }

    /// <summary>Verifies that probability ordinates can be modified.</summary>
    [TestMethod]
    public void ProbabilityOrdinates_CanBeModified()
    {
        var model = CreateTestModel();
        var analysis = new CompetingRiskAnalysis(model);
        analysis.ProbabilityOrdinates.Clear();

        analysis.ProbabilityOrdinates.Add(0.5);
        analysis.ProbabilityOrdinates.Add(0.1);
        analysis.ProbabilityOrdinates.Add(0.01);

        Assert.AreEqual(3, analysis.ProbabilityOrdinates.Count, "Should have 3 probability ordinates.");
    }

    #endregion

    #region Edge Cases

    /// <summary>Verifies that to X element creates valid xml structure.</summary>
    [TestMethod]
    public void ToXElement_CreatesValidXmlStructure()
    {
        var model = CreateTestModel();
        var analysis = new CompetingRiskAnalysis(model);

        var xElement = analysis.ToXElement();

        Assert.IsNotNull(xElement, "XElement should not be null.");
        Assert.AreEqual("CompetingRiskAnalysis", xElement.Name.LocalName, "Root element should be CompetingRiskAnalysis.");
        Assert.IsNotNull(xElement.Attribute("IsEstimated"), "Should have IsEstimated attribute.");
    }

    /// <summary>Verifies that bayesian analysis has default settings.</summary>
    [TestMethod]
    public void BayesianAnalysis_HasDefaultSettings()
    {
        var model = CreateTestModel();

        var analysis = new CompetingRiskAnalysis(model);

        Assert.IsTrue(analysis.BayesianAnalysis.Iterations > 0,
            "Iterations should have a default value.");
    }

    #endregion

    #region IUnivariateAnalysis Interface Tests

    /// <summary>Verifies that i univariate analysis is accessible when probability ordinates.</summary>
    [TestMethod]
    public void IUnivariateAnalysis_ProbabilityOrdinates_IsAccessible()
    {
        var model = CreateTestModel();
        var analysis = new CompetingRiskAnalysis(model);

        IUnivariateAnalysis iface = analysis;
        var ordinates = iface.ProbabilityOrdinates;

        Assert.IsNotNull(ordinates, "ProbabilityOrdinates should be accessible via interface.");
    }

    #endregion
}
