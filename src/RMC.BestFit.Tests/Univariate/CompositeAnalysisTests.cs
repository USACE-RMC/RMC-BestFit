using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Programmatic unit tests for the <c>CompositeAnalysis</c> class.
/// </summary>
/// <remarks>
/// Configuration, validation, weight aggregation, and serialization tests live here.
/// MCMC parameter-recovery and estimation tests live in <c>RMC.BestFit.Verification</c>.
/// </remarks>
[TestClass]
public class CompositeAnalysisTests
{
    #region Inline test fixtures

    private const int FixtureSize = 30;

    /// <summary>
    /// Deterministic Normal(15000, 5000) flood-like fixture. Inline (generated from a fixed RNG seed) so
    /// this file doesn't depend on the Verification project's <c>TestData</c>.
    /// </summary>
    private static readonly double[] InlineFloodData = new Normal(15000.0, 5000.0)
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
        for (int i = 0; i < InlineFloodData.Length; i++)
        {
            df.ExactSeries.Add(new ExactData(1990 + i, InlineFloodData[i]));
        }
        return df;
    }

    /// <summary>
    /// Creates test Univariate Analysis.
    /// </summary>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static UnivariateAnalysis CreateTestUnivariateAnalysis()
    {
        var df = CreateTestDataFrame();
        var dist = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        return new UnivariateAnalysis(dist);
    }

    /// <summary>
    /// Creates test Analysis.
    /// </summary>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static CompositeAnalysis CreateTestAnalysis()
    {
        var analysis1 = CreateTestUnivariateAnalysis();
        var analysis2 = CreateTestUnivariateAnalysis();

        var weightedAnalyses = new List<WeightedUnivariateAnalysis>
        {
            new WeightedUnivariateAnalysis { UnivariateAnalysis = analysis1, Weight = 0.5 },
            new WeightedUnivariateAnalysis { UnivariateAnalysis = analysis2, Weight = 0.5 }
        };

        return new CompositeAnalysis(weightedAnalyses);
    }

    #endregion

    #region Constructor Tests

    /// <summary>Verifies that constructor empty initializes correctly.</summary>
    [TestMethod]
    public void Constructor_Empty_InitializesCorrectly()
    {
        var analysis = new CompositeAnalysis();

        Assert.IsNotNull(analysis.Analyses, "Analyses should not be null.");
        Assert.AreEqual(0, analysis.Analyses.Count, "Analyses count should be 0.");
        Assert.IsNotNull(analysis.ProbabilityOrdinates, "ProbabilityOrdinates should not be null.");
        Assert.IsNotNull(analysis.BayesianAnalysis, "BayesianAnalysis should not be null.");
        Assert.IsFalse(analysis.IsEstimated, "IsEstimated should be false initially.");
    }

    /// <summary>Verifies that constructor with analyses initializes correctly.</summary>
    [TestMethod]
    public void Constructor_WithAnalyses_InitializesCorrectly()
    {
        var univariateAnalysis = CreateTestUnivariateAnalysis();
        var weightedAnalyses = new List<WeightedUnivariateAnalysis>
        {
            new WeightedUnivariateAnalysis { UnivariateAnalysis = univariateAnalysis, Weight = 1.0 }
        };

        var analysis = new CompositeAnalysis(weightedAnalyses);

        Assert.IsNotNull(analysis.Analyses, "Analyses should not be null.");
        Assert.AreEqual(1, analysis.Analyses.Count, "Analyses count should be 1.");
        Assert.IsFalse(analysis.IsEstimated, "IsEstimated should be false initially.");
    }

    #endregion

    #region XML Serialization Tests

    /// <summary>Verifies that xml serialization preserves configuration for round trip.</summary>
    [TestMethod]
    public void XmlSerialization_RoundTrip_PreservesConfiguration()
    {
        var analysis = new CompositeAnalysis();
        analysis.ProbabilityOrdinates.Clear();
        analysis.ProbabilityOrdinates.Add(0.5);
        analysis.ProbabilityOrdinates.Add(0.1);
        analysis.ProbabilityOrdinates.Add(0.01);

        var xElement = analysis.ToXElement();
        var restoredAnalysis = new CompositeAnalysis(xElement);

        Assert.IsNotNull(restoredAnalysis, "Restored analysis should not be null.");
        Assert.AreEqual(analysis.ProbabilityOrdinates.Count, restoredAnalysis.ProbabilityOrdinates.Count,
            "Probability ordinates count should be preserved.");
    }

    /// <summary>Verifies that constructor throws when with null X element.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_WithNullXElement_ThrowsArgumentNullException()
    {
        _ = new CompositeAnalysis(null!);
    }

    #endregion

    #region Validation Tests

    /// <summary>Verifies that validate returns invalid when with empty analyses.</summary>
    [TestMethod]
    public void Validate_WithEmptyAnalyses_ReturnsInvalid()
    {
        var analysis = new CompositeAnalysis();

        var (isValid, _) = analysis.Validate();

        Assert.IsFalse(isValid, "Validation should fail for empty analyses.");
    }

    /// <summary>Verifies that validate returns invalid when with unestimated sub analyses.</summary>
    [TestMethod]
    public void Validate_WithUnestimatedSubAnalyses_ReturnsInvalid()
    {
        // Composite validation requires every sub-analysis to be estimated; this is a
        // documented design invariant of CompositeAnalysis. The "happy path" validation
        // test would need real MCMC and lives in RMC.BestFit.Verification.
        var analysis = CreateTestAnalysis();

        var (isValid, messages) = analysis.Validate();

        Assert.IsFalse(isValid, "Validation should fail when sub-analyses are not estimated.");
        Assert.IsTrue(messages.Any(m => m.Contains("invalid or requires estimation")),
            "Expected the un-estimated sub-analysis message.");
    }

    #endregion

    #region ClearResults Tests

    /// <summary>Verifies that clear results resets all results.</summary>
    [TestMethod]
    public void ClearResults_ResetsAllResults()
    {
        var analysis = CreateTestAnalysis();

        analysis.ClearResults();

        Assert.IsFalse(analysis.IsEstimated, "IsEstimated should be false after clearing.");
    }

    /// <summary>Verifies that credible interval width changes clear composite results.</summary>
    [TestMethod]
    public void BayesianAnalysis_CredibleIntervalWidthChange_ClearsResults()
    {
        var analysis = CreateTestAnalysis();
        analysis.RestoreAnalysisResults(new UncertaintyAnalysisResults
        {
            ModeCurve = new[] { 1.0 }
        });

        analysis.BayesianAnalysis.CredibleIntervalWidth = 0.95;

        Assert.IsFalse(analysis.IsEstimated, "CI change should require a rerun.");
        Assert.IsNull(analysis.AnalysisResults, "CI change should clear derived composite results.");
    }

    /// <summary>Verifies that point estimator changes do not clear composite results.</summary>
    [TestMethod]
    public async Task BayesianAnalysis_PointEstimatorChange_DoesNotClearResults()
    {
        var analysis = CreateTestAnalysis();
        analysis.RestoreAnalysisResults(new UncertaintyAnalysisResults
        {
            ModeCurve = new[] { 1.0 }
        });
        var originalResults = analysis.AnalysisResults;

        analysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        await Task.Delay(100);

        Assert.IsTrue(analysis.IsEstimated, "PointEstimator change should preserve the estimated state.");
        Assert.AreSame(originalResults, analysis.AnalysisResults, "PointEstimator change should not clear results.");
    }

    #endregion

    #region Property Tests

    /// <summary>Verifies that analyses can be modified.</summary>
    [TestMethod]
    public void Analyses_CanBeModified()
    {
        var analysis = new CompositeAnalysis();
        var univariateAnalysis = CreateTestUnivariateAnalysis();

        analysis.Analyses.Add(new WeightedUnivariateAnalysis
        {
            UnivariateAnalysis = univariateAnalysis,
            Weight = 1.0
        });

        Assert.AreEqual(1, analysis.Analyses.Count, "Analyses count should be 1 after adding.");
    }

    /// <summary>Verifies that composite distribution type can be set.</summary>
    [TestMethod]
    public void CompositeDistributionType_CanBeSet()
    {
        var analysis = new CompositeAnalysis();

        analysis.CompositeDistributionType = CompositeType.Mixture;

        Assert.AreEqual(CompositeType.Mixture, analysis.CompositeDistributionType);
    }

    /// <summary>Verifies that model average method can be set.</summary>
    [TestMethod]
    public void ModelAverageMethod_CanBeSet()
    {
        var analysis = new CompositeAnalysis
        {
            CompositeDistributionType = CompositeType.ModelAverage
        };

        analysis.ModelAverageMethod = AverageMethod.AIC;

        Assert.AreEqual(AverageMethod.AIC, analysis.ModelAverageMethod);
    }

    #endregion

    #region CancelAnalysis Tests

    /// <summary>Verifies that cancel analysis does not throw for when not running.</summary>
    [TestMethod]
    public void CancelAnalysis_WhenNotRunning_DoesNotThrow()
    {
        var analysis = CreateTestAnalysis();

        analysis.CancelAnalysis();
    }

    #endregion

    #region Event Tests

    /// <summary>Verifies that run async throws when with unestimated sub analyses.</summary>
    [TestMethod]
    public async Task RunAsync_WithUnestimatedSubAnalyses_ThrowsInvalidOperationException()
    {
        // Composite analysis is a weighted aggregation over already-fitted sub-analyses;
        // it has no MCMC chain of its own. RunAsync validates BEFORE firing AnalysisStarting,
        // so an un-estimated sub-analysis must throw InvalidOperationException. Verifying the
        // event lifecycle for a "happy path" requires a real MCMC fit on each sub-analysis
        // (lives in RMC.BestFit.Verification).
        var analysis = CreateTestAnalysis();

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await analysis.RunAsync());
    }

    #endregion

    #region Edge Cases

    /// <summary>Verifies that to X element creates valid xml structure.</summary>
    [TestMethod]
    public void ToXElement_CreatesValidXmlStructure()
    {
        var analysis = new CompositeAnalysis();

        var xElement = analysis.ToXElement();

        Assert.IsNotNull(xElement, "XElement should not be null.");
        Assert.AreEqual("CompositeAnalysis", xElement.Name.LocalName, "Root element should be CompositeAnalysis.");
    }

    /// <summary>Verifies that probability ordinates has default values.</summary>
    [TestMethod]
    public void ProbabilityOrdinates_HasDefaultValues()
    {
        var analysis = new CompositeAnalysis();

        Assert.IsNotNull(analysis.ProbabilityOrdinates, "ProbabilityOrdinates should not be null.");
        Assert.IsTrue(analysis.ProbabilityOrdinates.Count > 0, "Should have default probability ordinates.");
    }

    #endregion

    #region PropertyChanged Tests

    /// <summary>Verifies that is estimated does not throw for change.</summary>
    [TestMethod]
    public void IsEstimated_Change_DoesNotThrow()
    {
        var analysis = CreateTestAnalysis();
        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(CompositeAnalysis.IsEstimated))
                propertyChanged = true;
        };

        analysis.ClearResults();

        Assert.IsFalse(analysis.IsEstimated, "IsEstimated should be false.");
        // Note: propertyChanged may or may not be raised depending on initial state
        _ = propertyChanged;
    }

    #endregion
}
