using Numerics;
using Numerics.Data;
using Numerics.Distributions;
using Numerics.Distributions.Copulas;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using System.Xml.Linq;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Bivariate;

/// <summary>
/// Programmatic unit tests for the <c>BivariateAnalysis</c> class.
/// Constructors, property round-trips, validation, serialization, ClearResults,
/// event-routing, and copula-type configuration. No MCMC chains are run here —
/// computational verification (parameter recovery, RunAsync end-to-end) lives in
/// <c>RMC.BestFit.Verification/Bivariate/BivariateAnalysisTests.cs</c>.
/// </summary>
/// <remarks>
/// <para>
///     <b>Authors:</b>
/// </para>
/// <list type="bullet">
///     <item>Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil</item>
/// </list>
/// </remarks>
[TestClass]
public class BivariateAnalysisTests
{
    #region Inline test fixtures

    /// <summary>Fixed sample size for the synthetic marginals.</summary>
    private const int FixtureSize = 200;

    /// <summary>
    /// Deterministic Normal(100, 15) fixture for the X marginal. Generated inline from a
    /// fixed RNG seed so this file does not depend on the Verification project's
    /// <c>TestData</c> static class.
    /// </summary>
    private static readonly double[] InlineXData = new Normal(100.0, 15.0)
        .GenerateRandomValues(FixtureSize, 12345);

    /// <summary>
    /// Deterministic Gumbel(50, 15) fixture for the Y marginal.
    /// </summary>
    private static readonly double[] InlineYData = new Gumbel(50.0, 15.0)
        .GenerateRandomValues(FixtureSize, 67890);

    /// <summary>
    /// Creates a pair of test marginal distributions for bivariate analysis.
    /// </summary>
    private static (UnivariateDistribution marginalX, UnivariateDistribution marginalY) CreateMarginals(int? count = null)
    {
        int n = count ?? FixtureSize;

        var dfX = new BestFitDataFrame { ExactSeries = new ExactSeries(InlineXData.Take(n).ToArray()) };
        var marginalX = new UnivariateDistribution(dfX, UnivariateDistributionType.Normal);

        var dfY = new BestFitDataFrame { ExactSeries = new ExactSeries(InlineYData.Take(n).ToArray()) };
        var marginalY = new UnivariateDistribution(dfY, UnivariateDistributionType.Gumbel);

        return (marginalX, marginalY);
    }

    /// <summary>
    /// Creates a test <c>BivariateDistribution</c> with Normal copula.
    /// </summary>
    private static BivariateDistribution CreateTestBivariateDistribution(int? count = null)
    {
        var (marginalX, marginalY) = CreateMarginals(count);
        return new BivariateDistribution(marginalX, marginalY, CopulaType.Normal);
    }

    /// <summary>
    /// Creates a test <c>BivariateDistribution</c> with a specified copula type.
    /// </summary>
    private static BivariateDistribution CreateTestBivariateDistribution(CopulaType copulaType, int? count = null)
    {
        var (marginalX, marginalY) = CreateMarginals(count);
        return new BivariateDistribution(marginalX, marginalY, copulaType);
    }

    /// <summary>
    /// Creates a <c>BivariateAnalysis</c> with the BayesianAnalysis configuration set
    /// to satisfy <c>BayesianAnalysis.Validate()</c>'s lower bound on chain count.
    /// </summary>
    /// <remarks>
    /// The default sampler (<c>DEMCzs</c>) sets <c>NumberOfChains = max(3, min(20, 2*d))</c>.
    /// For a bivariate model with 1 copula parameter (d=1), the default lands at 3, which
    /// fails the validator's "must be between 4 and 20" rule. Tests that exercise
    /// validation must therefore raise the chain count to 4 explicitly.
    /// </remarks>
    private static BivariateAnalysis CreateTestAnalysis(BivariateDistribution bivariateDist)
    {
        var analysis = new BivariateAnalysis(bivariateDist);
        analysis.BayesianAnalysis.NumberOfChains = 4;
        return analysis;
    }

    /// <summary>
    /// Creates test XY ordinates for joint exceedance probability computation.
    /// </summary>
    private static UncertainOrderedPairedData CreateTestXYOrdinates()
    {
        var ordinates = new List<UncertainOrdinate>
        {
            new UncertainOrdinate(80, new Deterministic(40)),
            new UncertainOrdinate(100, new Deterministic(50)),
            new UncertainOrdinate(120, new Deterministic(60)),
            new UncertainOrdinate(140, new Deterministic(70)),
            new UncertainOrdinate(160, new Deterministic(80))
        };

        return new UncertainOrderedPairedData(
            ordinates,
            false, SortOrder.Ascending,
            false, SortOrder.Ascending,
            UnivariateDistributionType.Deterministic);
    }

    #endregion

    #region Constructor Tests

    /// <summary>
    /// Tests that the constructor properly initializes the analysis with a bivariate distribution.
    /// </summary>
    [TestMethod]
    public void Constructor_WithBivariateDistribution_InitializesCorrectly()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);

        var analysis = CreateTestAnalysis(bivariateDist);

        Assert.IsNotNull(analysis.BivariateDistribution, "BivariateDistribution should not be null.");
        Assert.IsNotNull(analysis.BayesianAnalysis, "BayesianAnalysis should not be null.");
        Assert.IsNotNull(analysis.XYOrdinates, "XYOrdinates should not be null.");
        Assert.IsFalse(analysis.IsEstimated, "IsEstimated should be false initially.");
        Assert.IsNull(analysis.AnalysisResults, "AnalysisResults should be null initially.");
    }

    /// <summary>
    /// Tests that the constructor throws <c>ArgumentNullException</c> when distribution is null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_WithNullDistribution_ThrowsArgumentNullException()
    {
        _ = new BivariateAnalysis(null!);
    }

    /// <summary>
    /// Tests that the BayesianAnalysis property references the correct model.
    /// </summary>
    [TestMethod]
    public void Constructor_BayesianAnalysis_HasCorrectModel()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);

        var analysis = CreateTestAnalysis(bivariateDist);

        Assert.AreSame(bivariateDist, analysis.BayesianAnalysis.Model,
            "BayesianAnalysis.Model should reference the bivariate distribution.");
    }

    /// <summary>
    /// Tests the constructor with different copula types.
    /// </summary>
    [TestMethod]
    [DataRow(CopulaType.Normal)]
    [DataRow(CopulaType.Frank)]
    [DataRow(CopulaType.Clayton)]
    [DataRow(CopulaType.Gumbel)]
    public void Constructor_WithVariousCopulaTypes_InitializesCorrectly(CopulaType copulaType)
    {
        var bivariateDist = CreateTestBivariateDistribution(copulaType, 100);

        var analysis = CreateTestAnalysis(bivariateDist);

        Assert.IsNotNull(analysis, "Analysis should be created successfully.");
        Assert.AreEqual(copulaType, analysis.BivariateDistribution.CopulaType,
            "Copula type should match.");
    }

    #endregion

    #region XML Serialization Tests

    /// <summary>
    /// Tests that the analysis can be serialized to XML and restored.
    /// </summary>
    [TestMethod]
    public void XmlSerialization_RoundTrip_PreservesConfiguration()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);
        var analysis = CreateTestAnalysis(bivariateDist);
        analysis.XYOrdinates = CreateTestXYOrdinates();

        var xElement = analysis.ToXElement();
        var restoredAnalysis = new BivariateAnalysis(bivariateDist, xElement);

        Assert.IsNotNull(restoredAnalysis, "Restored analysis should not be null.");
        Assert.IsNotNull(restoredAnalysis.XYOrdinates, "XY ordinates should be restored.");
    }

    /// <summary>
    /// Tests that the constructor throws when XElement is null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_WithNullXElement_ThrowsArgumentNullException()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);

        _ = new BivariateAnalysis(bivariateDist, null!);
    }

    /// <summary>
    /// Tests serialization with Bayesian analysis settings preserved.
    /// </summary>
    [TestMethod]
    public void XmlSerialization_WithBayesianSettings_PreservesSettings()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);
        var analysis = CreateTestAnalysis(bivariateDist);
        analysis.BayesianAnalysis.Iterations = 5000;
        analysis.BayesianAnalysis.WarmupIterations = 1000;

        var xElement = analysis.ToXElement();
        var restoredAnalysis = new BivariateAnalysis(bivariateDist, xElement);

        Assert.AreEqual(5000, restoredAnalysis.BayesianAnalysis.Iterations,
            "Iterations should be preserved.");
        Assert.AreEqual(1000, restoredAnalysis.BayesianAnalysis.WarmupIterations,
            "WarmupIterations should be preserved.");
    }

    /// <summary>
    /// Tests that ToXElement creates a valid XML structure.
    /// </summary>
    [TestMethod]
    public void ToXElement_CreatesValidXmlStructure()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);
        var analysis = CreateTestAnalysis(bivariateDist);

        var xElement = analysis.ToXElement();

        Assert.IsNotNull(xElement, "XElement should not be null.");
        Assert.AreEqual("BivariateAnalysis", xElement.Name.LocalName, "Root element should be BivariateAnalysis.");
        Assert.IsNotNull(xElement.Attribute("IsEstimated"), "Should have IsEstimated attribute.");
    }

    /// <summary>
    /// Tests that the IsEstimated attribute is correctly serialized.
    /// </summary>
    [TestMethod]
    public void ToXElement_IsEstimatedAttribute_CorrectlySet()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);
        var analysis = CreateTestAnalysis(bivariateDist);

        var xElement = analysis.ToXElement();
        var isEstimatedValue = xElement.Attribute("IsEstimated")?.Value;

        // XML serializer uses lowercase "false"; accept either casing for safety.
        Assert.IsTrue(string.Equals(isEstimatedValue, "False", StringComparison.OrdinalIgnoreCase),
            $"IsEstimated should be False for unestimated analysis (got '{isEstimatedValue}').");
    }

    /// <summary>
    /// Tests that XY ordinates serialization writes the current payload.
    /// </summary>
    /// <remarks>
    /// This test stays at the analysis boundary and verifies that
    /// <c>BivariateAnalysis.ToXElement()</c> embeds the same payload produced by the
    /// ordinate collection itself. Full <c>UncertainOrderedPairedData</c> XML
    /// round-trip behavior belongs with that type.
    /// </remarks>
    [TestMethod]
    public void ToXElement_XYOrdinates_WritesCurrentPayload()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);
        var analysis = CreateTestAnalysis(bivariateDist);
        var testOrdinates = CreateTestXYOrdinates();
        analysis.XYOrdinates = testOrdinates;

        var xElement = analysis.ToXElement();
        var xyElement = xElement.Elements()
            .FirstOrDefault(element => element.Name.LocalName != nameof(BayesianAnalysis));

        Assert.IsNotNull(xyElement, "XY ordinates should be serialized.");
        Assert.AreEqual(testOrdinates.SaveToXElement().ToString(SaveOptions.DisableFormatting),
            xyElement.ToString(SaveOptions.DisableFormatting),
            "Serialized XY ordinates should match the current ordinate payload.");
    }

    #endregion

    #region Validation Tests

    /// <summary>
    /// Tests that Validate returns valid for a properly configured analysis.
    /// </summary>
    [TestMethod]
    public void Validate_WithValidConfiguration_ReturnsValid()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);
        var analysis = CreateTestAnalysis(bivariateDist);

        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Validation should pass. Messages: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests that Validate propagates distribution validation messages.
    /// </summary>
    [TestMethod]
    public void Validate_PropagatesDistributionValidation()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);
        var analysis = CreateTestAnalysis(bivariateDist);

        var (isValid, _) = analysis.Validate();

        Assert.IsTrue(isValid, "Validation should pass for valid distribution.");
    }

    /// <summary>
    /// Tests that Validate propagates BayesianAnalysis validation messages.
    /// </summary>
    [TestMethod]
    public void Validate_PropagatesBayesianAnalysisValidation()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);
        var analysis = CreateTestAnalysis(bivariateDist);

        var (isValid, _) = analysis.Validate();

        Assert.IsTrue(isValid, "BayesianAnalysis validation should pass.");
    }

    /// <summary>
    /// Tests validation with each supported copula type.
    /// </summary>
    [TestMethod]
    [DataRow(CopulaType.Normal)]
    [DataRow(CopulaType.Frank)]
    [DataRow(CopulaType.Clayton)]
    [DataRow(CopulaType.Gumbel)]
    public void Validate_WithDifferentCopulaTypes_ReturnsValid(CopulaType copulaType)
    {
        var bivariateDist = CreateTestBivariateDistribution(copulaType, 100);
        var analysis = CreateTestAnalysis(bivariateDist);

        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Validation should pass for {copulaType}. Messages: {string.Join(", ", messages)}");
    }

    #endregion

    #region ClearResults Tests

    /// <summary>
    /// Tests that ClearResults resets all analysis results.
    /// </summary>
    [TestMethod]
    public void ClearResults_ResetsAllResults()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);
        var analysis = CreateTestAnalysis(bivariateDist);

        analysis.ClearResults();

        Assert.IsFalse(analysis.IsEstimated, "IsEstimated should be false after clearing.");
        Assert.IsNull(analysis.AnalysisResults, "AnalysisResults should be null after clearing.");
    }

    /// <summary>
    /// Tests that ClearResults also clears BayesianAnalysis results.
    /// </summary>
    [TestMethod]
    public void ClearResults_ClearsBayesianAnalysisResults()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);
        var analysis = CreateTestAnalysis(bivariateDist);

        analysis.ClearResults();

        Assert.IsFalse(analysis.BayesianAnalysis.IsEstimated, "BayesianAnalysis.IsEstimated should be false.");
        Assert.IsNull(analysis.BayesianAnalysis.Results, "BayesianAnalysis.Results should be null.");
    }

    /// <summary>
    /// Tests that ClearResults raises PropertyChanged event for AnalysisResults.
    /// </summary>
    [TestMethod]
    public void ClearResults_RaisesPropertyChangedForAnalysisResults()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);
        var analysis = CreateTestAnalysis(bivariateDist);
        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(BivariateAnalysis.AnalysisResults))
                propertyChanged = true;
        };

        analysis.ClearResults();

        Assert.IsTrue(propertyChanged, "PropertyChanged should be raised for AnalysisResults.");
    }

    #endregion

    #region Property Change Tests

    /// <summary>
    /// Tests that XYOrdinates change triggers property change notification.
    /// </summary>
    [TestMethod]
    public void XYOrdinates_Change_RaisesPropertyChanged()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);
        var analysis = CreateTestAnalysis(bivariateDist);
        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(BivariateAnalysis.XYOrdinates))
                propertyChanged = true;
        };

        analysis.XYOrdinates = CreateTestXYOrdinates();

        Assert.IsTrue(propertyChanged, "PropertyChanged should be raised for XYOrdinates.");
    }

    /// <summary>
    /// Tests that setting XYOrdinates on an unestimated analysis leaves results empty.
    /// </summary>
    /// <remarks>
    /// <c>ReprocessOrClearXYOrdinates()</c> returns immediately until the analysis is
    /// estimated. This deterministic contract avoids relying on background timing or
    /// running an estimator in the fast test suite.
    /// </remarks>
    [TestMethod]
    public void XYOrdinates_Change_WhenUnestimated_KeepsResultsNull()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);
        var analysis = CreateTestAnalysis(bivariateDist);
        bool analysisResultsChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(BivariateAnalysis.AnalysisResults))
                analysisResultsChanged = true;
        };

        analysis.XYOrdinates = CreateTestXYOrdinates();

        Assert.IsFalse(analysis.IsEstimated, "The test analysis should remain unestimated.");
        Assert.IsNull(analysis.AnalysisResults, "AnalysisResults should remain null before estimation.");
        Assert.IsFalse(analysisResultsChanged,
            "AnalysisResults should not fire when a fresh, unestimated analysis has no results to clear.");
    }

    /// <summary>
    /// Tests that BivariateDistribution property is correctly accessible.
    /// </summary>
    [TestMethod]
    public void BivariateDistribution_Property_ReturnsCorrectReference()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);

        var analysis = CreateTestAnalysis(bivariateDist);

        Assert.AreSame(bivariateDist, analysis.BivariateDistribution,
            "BivariateDistribution property should return the correct reference.");
    }

    #endregion

    #region CancelAnalysis Tests

    /// <summary>
    /// Tests that CancelAnalysis can be called without error when not running.
    /// </summary>
    [TestMethod]
    public void CancelAnalysis_WhenNotRunning_DoesNotThrow()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);
        var analysis = CreateTestAnalysis(bivariateDist);

        analysis.CancelAnalysis();
    }

    /// <summary>
    /// Tests that CancelAnalysis also cancels the BayesianAnalysis.
    /// </summary>
    [TestMethod]
    public void CancelAnalysis_CancelsBayesianSimulation()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);
        var analysis = CreateTestAnalysis(bivariateDist);

        analysis.CancelAnalysis();
    }

    #endregion

    #region Copula Type Tests

    /// <summary>
    /// Tests analysis creation with Normal (Gaussian) copula.
    /// </summary>
    [TestMethod]
    public void Constructor_WithNormalCopula_InitializesCorrectly()
    {
        var bivariateDist = CreateTestBivariateDistribution(CopulaType.Normal, 100);

        var analysis = CreateTestAnalysis(bivariateDist);

        Assert.IsNotNull(analysis, "Analysis should be created.");
        Assert.IsInstanceOfType(analysis.BivariateDistribution.Copula, typeof(NormalCopula),
            "Copula should be NormalCopula.");
    }

    /// <summary>
    /// Tests analysis creation with Clayton copula.
    /// </summary>
    [TestMethod]
    public void Constructor_WithClaytonCopula_InitializesCorrectly()
    {
        var bivariateDist = CreateTestBivariateDistribution(CopulaType.Clayton, 100);

        var analysis = CreateTestAnalysis(bivariateDist);

        Assert.IsNotNull(analysis, "Analysis should be created.");
        Assert.IsInstanceOfType(analysis.BivariateDistribution.Copula, typeof(ClaytonCopula),
            "Copula should be ClaytonCopula.");
    }

    /// <summary>
    /// Tests analysis creation with Frank copula.
    /// </summary>
    [TestMethod]
    public void Constructor_WithFrankCopula_InitializesCorrectly()
    {
        var bivariateDist = CreateTestBivariateDistribution(CopulaType.Frank, 100);

        var analysis = CreateTestAnalysis(bivariateDist);

        Assert.IsNotNull(analysis, "Analysis should be created.");
        Assert.IsInstanceOfType(analysis.BivariateDistribution.Copula, typeof(FrankCopula),
            "Copula should be FrankCopula.");
    }

    /// <summary>
    /// Tests analysis creation with Gumbel copula.
    /// </summary>
    [TestMethod]
    public void Constructor_WithGumbelCopula_InitializesCorrectly()
    {
        var bivariateDist = CreateTestBivariateDistribution(CopulaType.Gumbel, 100);

        var analysis = CreateTestAnalysis(bivariateDist);

        Assert.IsNotNull(analysis, "Analysis should be created.");
        Assert.IsInstanceOfType(analysis.BivariateDistribution.Copula, typeof(GumbelCopula),
            "Copula should be GumbelCopula.");
    }

    /// <summary>
    /// Tests that all supported copula types can be validated successfully.
    /// </summary>
    [TestMethod]
    public void AllCopulaTypes_CanBeValidated()
    {
        var copulaTypes = new[] { CopulaType.Normal, CopulaType.Clayton, CopulaType.Frank, CopulaType.Gumbel };

        foreach (var copulaType in copulaTypes)
        {
            var bivariateDist = CreateTestBivariateDistribution(copulaType, 100);
            var analysis = CreateTestAnalysis(bivariateDist);
            var (isValid, messages) = analysis.Validate();

            Assert.IsTrue(isValid, $"{copulaType} should validate successfully. Messages: {string.Join(", ", messages)}");
        }
    }

    #endregion

    #region XY Ordinates Tests

    /// <summary>
    /// Tests that default XY ordinates are initialized.
    /// </summary>
    [TestMethod]
    public void XYOrdinates_DefaultInitialization_HasDefaultValues()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);

        var analysis = CreateTestAnalysis(bivariateDist);

        Assert.IsNotNull(analysis.XYOrdinates, "XYOrdinates should not be null.");
        Assert.IsTrue(analysis.XYOrdinates.Count >= 1, "Default XY ordinates should have at least one value.");
    }

    /// <summary>
    /// Tests that XY ordinates can be set.
    /// </summary>
    [TestMethod]
    public void XYOrdinates_CanBeSet()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);
        var analysis = CreateTestAnalysis(bivariateDist);
        var newOrdinates = CreateTestXYOrdinates();

        analysis.XYOrdinates = newOrdinates;

        Assert.AreEqual(5, analysis.XYOrdinates.Count, "Should have 5 XY ordinates.");
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Tests analysis with minimal data (edge case).
    /// </summary>
    [TestMethod]
    public void Constructor_WithMinimalData_InitializesCorrectly()
    {
        var bivariateDist = CreateTestBivariateDistribution(30);

        var analysis = CreateTestAnalysis(bivariateDist);

        Assert.IsNotNull(analysis, "Analysis should be created with minimal data.");
        var (isValid, _) = analysis.Validate();
        Assert.IsTrue(isValid, "Analysis should validate with minimal data.");
    }

    /// <summary>
    /// Tests that validation is correctly performed with small sample sizes.
    /// </summary>
    [TestMethod]
    public void Validate_WithSmallSample_ReturnsValid()
    {
        var bivariateDist = CreateTestBivariateDistribution(50);
        var analysis = CreateTestAnalysis(bivariateDist);

        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Should validate with 50 observations. Messages: {string.Join(", ", messages)}");
    }

    #endregion

    #region Integration Tests

    /// <summary>
    /// Tests that BayesianAnalysis settings can be configured.
    /// </summary>
    [TestMethod]
    public void BayesianAnalysis_SettingsCanBeConfigured()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);
        var analysis = CreateTestAnalysis(bivariateDist);

        analysis.BayesianAnalysis.Iterations = 10000;
        analysis.BayesianAnalysis.WarmupIterations = 2000;
        analysis.BayesianAnalysis.ThinningInterval = 2;

        Assert.AreEqual(10000, analysis.BayesianAnalysis.Iterations);
        Assert.AreEqual(2000, analysis.BayesianAnalysis.WarmupIterations);
        Assert.AreEqual(2, analysis.BayesianAnalysis.ThinningInterval);
    }

    /// <summary>
    /// Tests that changing copula type on the distribution clears results.
    /// </summary>
    [TestMethod]
    public void CopulaTypeChange_ClearsResults()
    {
        var bivariateDist = CreateTestBivariateDistribution(CopulaType.Normal, 100);
        var analysis = CreateTestAnalysis(bivariateDist);
        bool resultsClearedEventRaised = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(BivariateAnalysis.AnalysisResults) ||
                e.PropertyName == "CopulaType")
                resultsClearedEventRaised = true;
        };

        analysis.BivariateDistribution.CopulaType = CopulaType.Frank;

        Assert.IsNull(analysis.AnalysisResults, "Results should be cleared when copula type changes.");
        Assert.IsTrue(resultsClearedEventRaised, "PropertyChanged should be raised.");
    }

    /// <summary>
    /// Tests that the analysis handles property changes on BayesianAnalysis.
    /// </summary>
    [TestMethod]
    public void BayesianAnalysisPropertyChange_PropagatesCorrectly()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);
        var analysis = CreateTestAnalysis(bivariateDist);
        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(BayesianAnalysis.Iterations))
                propertyChanged = true;
        };

        analysis.BayesianAnalysis.Iterations = 5000;

        Assert.IsTrue(propertyChanged, "Property change should propagate from BayesianAnalysis.");
    }

    /// <summary>
    /// Tests analysis with different marginal distribution types.
    /// </summary>
    [TestMethod]
    public void Analysis_WithDifferentMarginalTypes_ValidatesCorrectly()
    {
        var dfX = new BestFitDataFrame { ExactSeries = new ExactSeries(InlineXData.Take(100).ToArray()) };
        var marginalX = new UnivariateDistribution(dfX, UnivariateDistributionType.Normal);

        var dfY = new BestFitDataFrame { ExactSeries = new ExactSeries(InlineYData.Take(100).ToArray()) };
        var marginalY = new UnivariateDistribution(dfY, UnivariateDistributionType.GeneralizedExtremeValue);

        var bivariateDist = new BivariateDistribution(marginalX, marginalY, CopulaType.Normal);
        var analysis = CreateTestAnalysis(bivariateDist);

        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Should validate with different marginal types. Messages: {string.Join(", ", messages)}");
    }

    #endregion

    #region AnalysisResults Tests

    /// <summary>
    /// Tests that AnalysisResults is null before estimation.
    /// </summary>
    [TestMethod]
    public void AnalysisResults_BeforeEstimation_IsNull()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);
        var analysis = CreateTestAnalysis(bivariateDist);

        Assert.IsNull(analysis.AnalysisResults, "AnalysisResults should be null before estimation.");
    }

    /// <summary>
    /// Tests that IsEstimated is false initially.
    /// </summary>
    [TestMethod]
    public void IsEstimated_BeforeEstimation_IsFalse()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);
        var analysis = CreateTestAnalysis(bivariateDist);

        Assert.IsFalse(analysis.IsEstimated, "IsEstimated should be false initially.");
    }

    #endregion

    #region Model Property Change Handler Tests

    /// <summary>
    /// Tests that parameter changes on the model clear results.
    /// </summary>
    [TestMethod]
    public void ModelParameterChange_ClearsResults()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);
        var analysis = CreateTestAnalysis(bivariateDist);

        if (bivariateDist.Parameters != null && bivariateDist.Parameters.Count > 0)
        {
            bivariateDist.Parameters[0].Value = bivariateDist.Parameters[0].Value + 0.1;

            Assert.IsNull(analysis.AnalysisResults, "Results should be cleared when parameters change.");
        }
    }

    #endregion

    #region Bayesian Analysis Settings Tests

    /// <summary>
    /// Tests that default simulation options are set correctly.
    /// </summary>
    [TestMethod]
    public void BayesianAnalysis_DefaultSettings_AreValid()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);
        var analysis = CreateTestAnalysis(bivariateDist);

        Assert.IsTrue(analysis.BayesianAnalysis.Iterations > 0,
            "Iterations should be positive.");
        Assert.IsTrue(analysis.BayesianAnalysis.WarmupIterations >= 0,
            "WarmupIterations should be non-negative.");
    }

    /// <summary>
    /// Tests the PointEstimator can be set to PosteriorMean.
    /// </summary>
    [TestMethod]
    public void BayesianAnalysis_PointEstimator_CanBeSetToPosteriorMean()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);
        var analysis = CreateTestAnalysis(bivariateDist);

        analysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMean;

        Assert.AreEqual(BayesianAnalysis.PointEstimateType.PosteriorMean,
            analysis.BayesianAnalysis.PointEstimator,
            "PointEstimator should be set to PosteriorMean.");
    }

    /// <summary>
    /// Tests the PointEstimator can be set to PosteriorMode (MAP).
    /// </summary>
    [TestMethod]
    public void BayesianAnalysis_PointEstimator_CanBeSetToMAP()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);
        var analysis = CreateTestAnalysis(bivariateDist);

        analysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;

        Assert.AreEqual(BayesianAnalysis.PointEstimateType.PosteriorMode,
            analysis.BayesianAnalysis.PointEstimator,
            "PointEstimator should be set to PosteriorMode.");
    }

    /// <summary>
    /// Tests that CredibleIntervalWidth can be configured.
    /// </summary>
    [TestMethod]
    public void BayesianAnalysis_CredibleIntervalWidth_CanBeSet()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);
        var analysis = CreateTestAnalysis(bivariateDist);

        analysis.BayesianAnalysis.CredibleIntervalWidth = 0.90;

        Assert.AreEqual(0.90, analysis.BayesianAnalysis.CredibleIntervalWidth, 1e-10,
            "CredibleIntervalWidth should be 0.90.");
    }

    #endregion
}
