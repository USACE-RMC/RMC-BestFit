using System.ComponentModel;
using System.Reflection;
using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Tests for the null-child validation propagation and the EstimateModelWeights
/// cascade-suppression in <c>CompositeAnalysis</c>.
/// </summary>
/// <remarks>
/// <para>
/// Fix C: when a UI sub-distribution selection goes null (the user deletes the chosen
/// child analysis or clears the data-grid combo), the model-layer
/// <c>CompositeAnalysis.Validate</c> must produce an error message — previously
/// the UI filtered nulls before syncing to the model layer, so the model-layer Validate
/// saw an empty Analyses collection and reported nothing.
/// </para>
/// <para>
/// Fix D: changing the <c>CompositeAnalysis.ModelAverageMethod</c> mutates each
/// child wua's Weight; without suppression, every Weight write triggered a
/// <c>CompositeAnalysis.ClearResults</c> via
/// <c>WeightedAnalysis_PropertyChanged</c>, producing N+1 AnalysisResults PropertyChanged
/// events for an N-child composite. Each event drove a full UpdateFrequencyPlot rebuild
/// in the App, producing visible flicker and a wait-cursor flash per child. The
/// _isEstimatingWeights guard collapses the cascade to exactly one ClearResults at the
/// outer caller's invocation site.
/// </para>
/// </remarks>
[TestClass]
public class CompositeNullChildAndCascadeTests
{
    #region Inline test fixtures

    private static readonly double[] InlineFloodData = new Normal(15000.0, 5000.0)
        .GenerateRandomValues(30, 12345);

    /// <summary>
    /// Creates data Frame.
    /// </summary>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static BestFitDataFrame CreateDataFrame()
    {
        var df = new BestFitDataFrame();
        for (int i = 0; i < InlineFloodData.Length; i++)
            df.ExactSeries.Add(new ExactData(1990 + i, InlineFloodData[i]));
        return df;
    }

    /// <summary>
    /// Creates fit Child.
    /// </summary>
    /// <param name="mapValues">The MAP parameter values assigned to the child analysis.</param>
    /// <param name="aic">The Akaike information criterion value assigned to the child analysis.</param>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static UnivariateAnalysis CreateFitChild(double[] mapValues, double aic)
    {
        var dist = new UnivariateDistribution(CreateDataFrame(), UnivariateDistributionType.Normal);
        var analysis = new UnivariateAnalysis(dist);

        var output = new List<ParameterSet>(50);
        for (int i = 0; i < 50; i++)
            output.Add(new ParameterSet((double[])mapValues.Clone(), 0.0));
        analysis.BayesianAnalysis.OutputLength = 50;
        analysis.BayesianAnalysis.SetCustomMCMCResults(
            new MCMCResults(new ParameterSet((double[])mapValues.Clone(), 0.0), output, 0.10),
            skipInformationCriteria: true);

        var isEstField = typeof(AnalysisBase).GetField("_isEstimated",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        isEstField.SetValue(analysis, true);

        var resultsProp = typeof(UnivariateAnalysis).GetProperty("AnalysisResults",
            BindingFlags.Instance | BindingFlags.Public)!;
        resultsProp.SetValue(analysis, new UncertaintyAnalysisResults { AIC = aic });
        return analysis;
    }

    #endregion

    #region Fix C — null child validation

    /// <summary>
    /// A composite that contains a <c>WeightedUnivariateAnalysis</c> with
    /// <c>WeightedUnivariateAnalysis.UnivariateAnalysis</c> set to <c>null</c>
    /// must report an error from <c>CompositeAnalysis.Validate</c>. The error
    /// message originates from <c>WeightedUnivariateAnalysis.Validate</c>.
    /// </summary>
    [TestMethod]
    public void Validate_WithNullChildAnalysis_ReturnsInvalidWithMessage()
    {
        var fitChild = CreateFitChild(new[] { 15000.0, 5000.0 }, aic: 100.0);
        var composite = new CompositeAnalysis();
        composite.ProbabilityOrdinates.Add(0.01);
        composite.ProbabilityOrdinates.Add(0.5);
        composite.ProbabilityOrdinates.Add(0.99);
        composite.Analyses.Add(new WeightedUnivariateAnalysis(fitChild, weight: 0.5));
        // Second wua with null UnivariateAnalysis — simulates the UI scenario where
        // the user deletes the chosen sub-distribution and the data-grid combo
        // selection clears.
        composite.Analyses.Add(new WeightedUnivariateAnalysis { Weight = 0.5 });

        var (isValid, messages) = composite.Validate();

        Assert.IsFalse(isValid, "A composite with a null child must not validate.");
        Assert.IsTrue(messages.Exists(m =>
                m.Contains("invalid", System.StringComparison.OrdinalIgnoreCase) &&
                m.Contains("univariate analysis", System.StringComparison.OrdinalIgnoreCase)),
            "Error message should call out the missing/invalid univariate analysis selection. " +
            "Actual messages: " + string.Join("; ", messages));
    }

    /// <summary>
    /// Setting a previously-valid child slot back to null after estimation transitions
    /// the composite to invalid with a per-child error message.
    /// </summary>
    [TestMethod]
    public void Validate_ChildClearedToNull_AfterValidConfiguration_ReportsInvalid()
    {
        var fitChild = CreateFitChild(new[] { 15000.0, 5000.0 }, aic: 100.0);
        var composite = new CompositeAnalysis();
        composite.ProbabilityOrdinates.Add(0.01);
        composite.ProbabilityOrdinates.Add(0.5);
        composite.ProbabilityOrdinates.Add(0.99);
        var wua = new WeightedUnivariateAnalysis(fitChild, 1.0);
        composite.Analyses.Add(wua);

        // Clear the child reference (simulates the UI deletion path) and re-validate.
        wua.UnivariateAnalysis = null!;
        var (validAfter, messages) = composite.Validate();

        Assert.IsFalse(validAfter, "After clearing the child, the composite must be invalid.");
        Assert.IsTrue(messages.Exists(m =>
                m.Contains("invalid", System.StringComparison.OrdinalIgnoreCase) &&
                m.Contains("univariate analysis", System.StringComparison.OrdinalIgnoreCase)),
            "Validation must surface the per-child 'select a univariate analysis' error. " +
            "Actual messages: " + string.Join("; ", messages));
    }

    #endregion

    #region Fix D — EstimateModelWeights cascade suppression

    /// <summary>
    /// Counts the number of <c>"AnalysisResults"</c> PropertyChanged events fired by the
    /// composite during a single <c>CompositeAnalysis.ModelAverageMethod</c>
    /// change. Pre-fix this was N+1 for an N-child composite (one per per-child Weight
    /// write plus the outer ClearResults). Post-fix it must collapse to exactly 1.
    /// </summary>
    [TestMethod]
    public void ModelAverageMethodChange_FiresExactlyOneAnalysisResultsEvent()
    {
        // Two fit children — without the cascade-suppression guard, this would fire
        // 3 AnalysisResults events (2 from per-child Weight writes inside
        // EstimateModelWeights, 1 from the outer ClearResults).
        var childA = CreateFitChild(new[] { 15000.0, 5000.0 }, aic: 100.0);
        var childB = CreateFitChild(new[] { 16000.0, 5500.0 }, aic: 110.0);
        var composite = new CompositeAnalysis();
        composite.ProbabilityOrdinates.Add(0.01);
        composite.ProbabilityOrdinates.Add(0.5);
        composite.ProbabilityOrdinates.Add(0.99);
        composite.CompositeDistributionType = CompositeType.ModelAverage;
        composite.ModelAverageMethod = AverageMethod.AIC;
        composite.Analyses.Add(new WeightedUnivariateAnalysis(childA, 0.5));
        composite.Analyses.Add(new WeightedUnivariateAnalysis(childB, 0.5));
        composite.EstimateModelWeights();  // bootstrap weights

        int analysisResultsCount = 0;
        composite.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(CompositeAnalysis.AnalysisResults))
                analysisResultsCount++;
        };

        // Change the averaging method — this is the user-facing scenario that produced flicker.
        composite.ModelAverageMethod = AverageMethod.BIC;

        Assert.AreEqual(1, analysisResultsCount,
            "ModelAverageMethod change should fire exactly one AnalysisResults event " +
            $"(was {analysisResultsCount}). N+1 events indicate the per-child Weight cascade " +
            "is not being suppressed in EstimateModelWeights.");
    }

    /// <summary>
    /// User-driven Weight edits (Mixture mode, where the Weight column is editable) must
    /// still fire <c>CompositeAnalysis.ClearResults</c> — the
    /// _isEstimatingWeights guard only suppresses the cascade when EstimateModelWeights
    /// itself is the writer.
    /// </summary>
    [TestMethod]
    public void UserEditedWeight_OutsideEstimateModelWeights_StillClearsResults()
    {
        var childA = CreateFitChild(new[] { 15000.0, 5000.0 }, aic: 100.0);
        var childB = CreateFitChild(new[] { 16000.0, 5500.0 }, aic: 110.0);
        var composite = new CompositeAnalysis
        {
            CompositeDistributionType = CompositeType.Mixture
        };
        composite.ProbabilityOrdinates.Add(0.01);
        composite.ProbabilityOrdinates.Add(0.5);
        composite.ProbabilityOrdinates.Add(0.99);
        composite.Analyses.Add(new WeightedUnivariateAnalysis(childA, 0.5));
        composite.Analyses.Add(new WeightedUnivariateAnalysis(childB, 0.5));

        // Force IsEstimated=true on the composite so ClearResults' notification is observable.
        var isEstField = typeof(AnalysisBase).GetField("_isEstimated",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        isEstField.SetValue(composite, true);

        bool clearObserved = false;
        composite.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(CompositeAnalysis.IsEstimated))
                clearObserved = true;
        };

        // Simulate the user editing a Weight cell directly.
        composite.Analyses[0].Weight = 0.6;

        Assert.IsTrue(clearObserved,
            "A user-edited Weight (writer is NOT EstimateModelWeights) must still trigger " +
            "CompositeAnalysis.ClearResults so the composite is marked dirty.");
    }

    /// <summary>
    /// During the cascade-suppressed window (EstimateModelWeights is mutating weights),
    /// the per-Weight handler must NOT call ClearResults. After EstimateModelWeights
    /// returns, _isEstimatingWeights is reset so a subsequent user edit goes through
    /// the normal path.
    /// </summary>
    [TestMethod]
    public void IsEstimatingWeightsGuard_ResetsAfterEstimateModelWeights()
    {
        var childA = CreateFitChild(new[] { 15000.0, 5000.0 }, aic: 100.0);
        var childB = CreateFitChild(new[] { 16000.0, 5500.0 }, aic: 110.0);
        var composite = new CompositeAnalysis
        {
            CompositeDistributionType = CompositeType.ModelAverage,
            ModelAverageMethod = AverageMethod.AIC
        };
        composite.ProbabilityOrdinates.Add(0.01);
        composite.ProbabilityOrdinates.Add(0.5);
        composite.ProbabilityOrdinates.Add(0.99);
        composite.Analyses.Add(new WeightedUnivariateAnalysis(childA, 0.5));
        composite.Analyses.Add(new WeightedUnivariateAnalysis(childB, 0.5));

        // Flip into ModelAverage with EstimateModelWeights. After this returns, the
        // _isEstimatingWeights guard must be off so the next user-driven Weight edit
        // fires ClearResults normally.
        composite.EstimateModelWeights();

        // Force IsEstimated=true and observe that a follow-up Weight edit fires ClearResults.
        var isEstField = typeof(AnalysisBase).GetField("_isEstimated",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        isEstField.SetValue(composite, true);

        bool clearedAfter = false;
        composite.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(CompositeAnalysis.IsEstimated))
                clearedAfter = true;
        };

        composite.Analyses[0].Weight = composite.Analyses[0].Weight + 0.1;

        Assert.IsTrue(clearedAfter,
            "After EstimateModelWeights returns, _isEstimatingWeights must be reset to false " +
            "so subsequent user-driven Weight edits trigger ClearResults.");
    }

    #endregion
}
