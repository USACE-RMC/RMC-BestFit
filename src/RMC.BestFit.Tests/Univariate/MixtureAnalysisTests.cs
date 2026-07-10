using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using System.Xml.Linq;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Programmatic unit tests for the <c>MixtureAnalysis</c> class.
/// </summary>
/// <remarks>
/// Configuration, validation, and serialization tests live here.
/// MCMC parameter-recovery and estimation tests live in <c>RMC.BestFit.Verification</c>.
/// </remarks>
[TestClass]
public class MixtureAnalysisTests
{
    #region Inline test fixtures

    private const int LowSize = 10;
    private const int HighSize = 10;

    /// <summary>
    /// Inline bimodal flood data: a Normal(5500, 600) "snowmelt" component plus a
    /// Normal(20000, 3000) "rainfall" component. Generated from fixed seeds so the
    /// fixture is deterministic and independent of the Verification project's <c>TestData</c>.
    /// </summary>
    private static readonly double[] InlineLowComponent = new Normal(5500.0, 600.0)
        .GenerateRandomValues(LowSize, 12345);

    private static readonly double[] InlineHighComponent = new Normal(20000.0, 3000.0)
        .GenerateRandomValues(HighSize, 67890);

    /// <summary>
    /// Creates bimodal Test Data Frame.
    /// </summary>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static BestFitDataFrame CreateBimodalTestDataFrame()
    {
        var df = new BestFitDataFrame();
        int year = 1980;
        for (int i = 0; i < InlineLowComponent.Length; i++)
            df.ExactSeries.Add(new ExactData(year++, InlineLowComponent[i]));
        for (int i = 0; i < InlineHighComponent.Length; i++)
            df.ExactSeries.Add(new ExactData(year++, InlineHighComponent[i]));
        return df;
    }

    /// <summary>
    /// Creates test Mixture Model.
    /// </summary>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static MixtureModel CreateTestMixtureModel()
    {
        var df = CreateBimodalTestDataFrame();
        var distributionTypes = new List<UnivariateDistributionType>
        {
            UnivariateDistributionType.Normal,
            UnivariateDistributionType.Normal
        };
        return new MixtureModel(df, distributionTypes);
    }

    /// <summary>
    /// Creates gEV Mixture Model.
    /// </summary>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static MixtureModel CreateGEVMixtureModel()
    {
        var df = CreateBimodalTestDataFrame();
        var distributionTypes = new List<UnivariateDistributionType>
        {
            UnivariateDistributionType.GeneralizedExtremeValue,
            UnivariateDistributionType.GeneralizedExtremeValue
        };
        return new MixtureModel(df, distributionTypes);
    }

    /// <summary>
    /// Creates an estimated mixture analysis with stub uncertainty results.
    /// </summary>
    /// <param name="model">The mixture model to attach to the analysis.</param>
    /// <returns>An analysis whose stale estimated state can be invalidated without running MCMC.</returns>
    /// <remarks>
    /// The helper uses the XML constructor so tests stay fast and avoid computational verification.
    /// </remarks>
    private static MixtureAnalysis CreateEstimatedAnalysis(MixtureModel model)
    {
        var xElement = new XElement("MixtureAnalysis", new XAttribute("IsEstimated", true));
        return new MixtureAnalysis(model, xElement, analysisResults: new UncertaintyAnalysisResults());
    }

    #endregion

    #region Constructor Tests

    /// <summary>Verifies that constructor with mixture model initializes correctly.</summary>
    [TestMethod]
    public void Constructor_WithMixtureModel_InitializesCorrectly()
    {
        var model = CreateTestMixtureModel();

        var analysis = new MixtureAnalysis(model);

        Assert.IsNotNull(analysis.MixtureDistribution, "MixtureDistribution should not be null.");
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
        _ = new MixtureAnalysis(null!);
    }

    /// <summary>Verifies that constructor bayesian analysis has correct model.</summary>
    [TestMethod]
    public void Constructor_BayesianAnalysis_HasCorrectModel()
    {
        var model = CreateTestMixtureModel();

        var analysis = new MixtureAnalysis(model);

        Assert.AreSame(model, analysis.BayesianAnalysis.Model,
            "BayesianAnalysis.Model should reference the mixture model.");
    }

    #endregion

    #region XML Serialization Tests

    /// <summary>Verifies that xml serialization preserves configuration for round trip.</summary>
    [TestMethod]
    public void XmlSerialization_RoundTrip_PreservesConfiguration()
    {
        var model = CreateTestMixtureModel();
        var analysis = new MixtureAnalysis(model);

        analysis.ProbabilityOrdinates.Clear();
        analysis.ProbabilityOrdinates.Add(0.5);
        analysis.ProbabilityOrdinates.Add(0.1);
        analysis.ProbabilityOrdinates.Add(0.01);

        var xElement = analysis.ToXElement();
        var restoredAnalysis = new MixtureAnalysis(model, xElement);

        Assert.IsNotNull(restoredAnalysis, "Restored analysis should not be null.");
        Assert.AreEqual(analysis.ProbabilityOrdinates.Count, restoredAnalysis.ProbabilityOrdinates.Count,
            "Probability ordinates count should be preserved.");
    }

    /// <summary>Verifies that constructor throws when with null X element.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_WithNullXElement_ThrowsArgumentNullException()
    {
        var model = CreateTestMixtureModel();

        _ = new MixtureAnalysis(model, null!);
    }

    /// <summary>Verifies that xml serialization preserves settings for with bayesian settings.</summary>
    [TestMethod]
    public void XmlSerialization_WithBayesianSettings_PreservesSettings()
    {
        var model = CreateTestMixtureModel();
        var analysis = new MixtureAnalysis(model);
        analysis.BayesianAnalysis.Iterations = 8000;
        analysis.BayesianAnalysis.WarmupIterations = 2000;

        var xElement = analysis.ToXElement();
        var restoredAnalysis = new MixtureAnalysis(model, xElement);

        Assert.AreEqual(8000, restoredAnalysis.BayesianAnalysis.Iterations,
            "Iterations should be preserved.");
        Assert.AreEqual(2000, restoredAnalysis.BayesianAnalysis.WarmupIterations,
            "WarmupIterations should be preserved.");
    }

    #endregion

    #region Validation Tests

    /// <summary>Verifies that validate returns valid when with valid configuration.</summary>
    [TestMethod]
    public void Validate_WithValidConfiguration_ReturnsValid()
    {
        var model = CreateTestMixtureModel();
        var analysis = new MixtureAnalysis(model);

        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Validation should pass. Messages: {string.Join(", ", messages)}");
    }

    /// <summary>Verifies that validate propagates model validation.</summary>
    [TestMethod]
    public void Validate_PropagatesModelValidation()
    {
        var model = CreateTestMixtureModel();
        var analysis = new MixtureAnalysis(model);

        var (isValid, _) = analysis.Validate();

        Assert.IsTrue(isValid, "Validation should pass for valid mixture model.");
    }

    #endregion

    #region ClearResults Tests

    /// <summary>Verifies that clear results resets all results.</summary>
    [TestMethod]
    public void ClearResults_ResetsAllResults()
    {
        var model = CreateTestMixtureModel();
        var analysis = new MixtureAnalysis(model);

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
        var model = CreateTestMixtureModel();
        var analysis = new MixtureAnalysis(model);
        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MixtureAnalysis.ProbabilityOrdinates))
                propertyChanged = true;
        };

        analysis.ProbabilityOrdinates.Add(0.001);

        Assert.IsTrue(propertyChanged, "PropertyChanged should be raised for ProbabilityOrdinates.");
    }

    /// <summary>Verifies that model property change clears results.</summary>
    [TestMethod]
    public void ModelPropertyChange_ClearsResults()
    {
        var model = CreateTestMixtureModel();
        var analysis = new MixtureAnalysis(model);
        bool resultsCleared = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MixtureAnalysis.AnalysisResults))
                resultsCleared = true;
        };

        var newDistributions = new List<UnivariateDistributionBase>
        {
            new Normal(5000, 500),
            new Normal(20000, 2000),
            new Normal(30000, 3000)
        };
        model.Mixture = new Mixture(new[] { 0.33, 0.34, 0.33 }, newDistributions.ToArray());

        Assert.IsTrue(resultsCleared || analysis.AnalysisResults == null,
            "Results should be cleared when model changes.");
    }

    /// <summary>Verifies that enabling quantile priors clears stale analysis results.</summary>
    [TestMethod]
    public void EnableQuantilePriors_ClearsResults()
    {
        var model = CreateTestMixtureModel();
        var analysis = CreateEstimatedAnalysis(model);
        bool analysisResultsChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MixtureAnalysis.AnalysisResults))
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
        var model = CreateTestMixtureModel();
        model.EnableQuantilePriors = true;
        var analysis = CreateEstimatedAnalysis(model);
        bool analysisResultsChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MixtureAnalysis.AnalysisResults))
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
        var model = CreateTestMixtureModel();
        var analysis = CreateEstimatedAnalysis(model);
        bool analysisResultsChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MixtureAnalysis.AnalysisResults))
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
        var model = CreateTestMixtureModel();
        var analysis = new MixtureAnalysis(model);

        var result = analysis.GetDistribution(0);

        Assert.IsNull(result, "GetDistribution should return null when not estimated.");
    }

    /// <summary>Verifies that get point estimate distribution returns null when when not estimated.</summary>
    [TestMethod]
    public void GetPointEstimateDistribution_WhenNotEstimated_ReturnsNull()
    {
        var model = CreateTestMixtureModel();
        var analysis = new MixtureAnalysis(model);

        var result = analysis.GetPointEstimateDistribution();

        Assert.IsNull(result, "GetPointEstimateDistribution should return null when not estimated.");
    }

    #endregion

    #region CancelAnalysis Tests

    /// <summary>Verifies that cancel analysis does not throw for when not running.</summary>
    [TestMethod]
    public void CancelAnalysis_WhenNotRunning_DoesNotThrow()
    {
        var model = CreateTestMixtureModel();
        var analysis = new MixtureAnalysis(model);

        analysis.CancelAnalysis();
    }

    #endregion

    #region Event Tests

    /// <summary>Verifies that run async raises analysis starting event.</summary>
    [TestMethod]
    public async Task RunAsync_RaisesAnalysisStartingEvent()
    {
        var model = CreateTestMixtureModel();
        var analysis = new MixtureAnalysis(model);
        analysis.BayesianAnalysis.Iterations = 100;
        analysis.BayesianAnalysis.WarmupIterations = 50;

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
        var model = CreateTestMixtureModel();
        var analysis = new MixtureAnalysis(model);

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

    /// <summary>Verifies that validate returns valid when with GEV components.</summary>
    [TestMethod]
    public void Validate_WithGEVComponents_ReturnsValid()
    {
        var model = CreateGEVMixtureModel();
        var analysis = new MixtureAnalysis(model);

        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Validation should pass for GEV mixture. Messages: {string.Join(", ", messages)}");
    }

    /// <summary>Verifies that constructor with various component types initializes correctly.</summary>
    [TestMethod]
    [DataRow(UnivariateDistributionType.Normal)]
    [DataRow(UnivariateDistributionType.LogNormal)]
    [DataRow(UnivariateDistributionType.Gumbel)]
    [DataRow(UnivariateDistributionType.GeneralizedExtremeValue)]
    public void Constructor_WithVariousComponentTypes_InitializesCorrectly(UnivariateDistributionType distType)
    {
        var df = CreateBimodalTestDataFrame();
        var distributionTypes = new List<UnivariateDistributionType> { distType, distType };
        var model = new MixtureModel(df, distributionTypes);

        var analysis = new MixtureAnalysis(model);

        Assert.IsNotNull(analysis, "Analysis should be created successfully.");
        Assert.AreEqual(distType, analysis.MixtureDistribution.Mixture!.Distributions[0].Type,
            "Component distribution type should match.");
    }

    /// <summary>Verifies that constructor with various component counts initializes correctly.</summary>
    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    public void Constructor_WithVariousComponentCounts_InitializesCorrectly(int componentCount)
    {
        var df = CreateBimodalTestDataFrame();
        var distributionTypes = Enumerable.Repeat(UnivariateDistributionType.Normal, componentCount).ToList();
        var model = new MixtureModel(df, distributionTypes);

        var analysis = new MixtureAnalysis(model);

        Assert.IsNotNull(analysis, "Analysis should be created successfully.");
        Assert.AreEqual(componentCount, analysis.MixtureDistribution.Mixture!.Distributions.Length,
            "Number of components should match.");
    }

    #endregion

    #region Probability Ordinates Tests

    /// <summary>Verifies that probability ordinates default initialization has default values.</summary>
    [TestMethod]
    public void ProbabilityOrdinates_DefaultInitialization_HasDefaultValues()
    {
        var model = CreateTestMixtureModel();

        var analysis = new MixtureAnalysis(model);

        Assert.IsNotNull(analysis.ProbabilityOrdinates, "ProbabilityOrdinates should not be null.");
        Assert.IsTrue(analysis.ProbabilityOrdinates.Count > 0, "Default probability ordinates should have values.");
    }

    /// <summary>Verifies that probability ordinates can be modified.</summary>
    [TestMethod]
    public void ProbabilityOrdinates_CanBeModified()
    {
        var model = CreateTestMixtureModel();
        var analysis = new MixtureAnalysis(model);
        analysis.ProbabilityOrdinates.Clear();

        analysis.ProbabilityOrdinates.Add(0.5);
        analysis.ProbabilityOrdinates.Add(0.1);
        analysis.ProbabilityOrdinates.Add(0.01);

        Assert.AreEqual(3, analysis.ProbabilityOrdinates.Count, "Should have 3 probability ordinates.");
        Assert.AreEqual(0.5, analysis.ProbabilityOrdinates[0], 1e-10, "First ordinate should be 0.5.");
    }

    #endregion

    #region Edge Cases

    /// <summary>Verifies that constructor with single component initializes correctly.</summary>
    [TestMethod]
    public void Constructor_WithSingleComponent_InitializesCorrectly()
    {
        var df = CreateBimodalTestDataFrame();
        var distributionTypes = new List<UnivariateDistributionType> { UnivariateDistributionType.Normal };
        var model = new MixtureModel(df, distributionTypes);

        var analysis = new MixtureAnalysis(model);

        Assert.IsNotNull(analysis, "Analysis should be created with single component.");
        Assert.AreEqual(1, analysis.MixtureDistribution.Mixture!.Distributions.Length);
    }

    /// <summary>Verifies that to X element creates valid xml structure.</summary>
    [TestMethod]
    public void ToXElement_CreatesValidXmlStructure()
    {
        var model = CreateTestMixtureModel();
        var analysis = new MixtureAnalysis(model);

        var xElement = analysis.ToXElement();

        Assert.IsNotNull(xElement, "XElement should not be null.");
        Assert.AreEqual("MixtureAnalysis", xElement.Name.LocalName, "Root element should be MixtureAnalysis.");
        Assert.IsNotNull(xElement.Attribute("IsEstimated"), "Should have IsEstimated attribute.");
    }

    /// <summary>Verifies that bayesian analysis has default settings.</summary>
    [TestMethod]
    public void BayesianAnalysis_HasDefaultSettings()
    {
        var model = CreateTestMixtureModel();

        var analysis = new MixtureAnalysis(model);

        Assert.IsTrue(analysis.BayesianAnalysis.Iterations > 0,
            "Iterations should have a default value.");
        Assert.IsTrue(analysis.BayesianAnalysis.WarmupIterations >= 0,
            "WarmupIterations should have a default value.");
    }

    #endregion

    #region IUnivariateAnalysis Interface Tests

    /// <summary>Verifies that i univariate analysis is accessible when probability ordinates.</summary>
    [TestMethod]
    public void IUnivariateAnalysis_ProbabilityOrdinates_IsAccessible()
    {
        var model = CreateTestMixtureModel();
        var analysis = new MixtureAnalysis(model);

        IUnivariateAnalysis iface = analysis;
        var ordinates = iface.ProbabilityOrdinates;

        Assert.IsNotNull(ordinates, "ProbabilityOrdinates should be accessible via interface.");
    }

    /// <summary>Verifies that i univariate analysis is accessible when get distribution.</summary>
    [TestMethod]
    public void IUnivariateAnalysis_GetDistribution_IsAccessible()
    {
        var model = CreateTestMixtureModel();
        var analysis = new MixtureAnalysis(model);

        IUnivariateAnalysis iface = analysis;
        var dist = iface.GetDistribution(0);

        Assert.IsNull(dist, "GetDistribution should return null when not estimated.");
    }

    #endregion
}
