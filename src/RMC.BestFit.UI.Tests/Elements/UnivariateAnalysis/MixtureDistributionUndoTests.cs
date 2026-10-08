using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Distributions;
using RMC.BestFit.UI;
using System;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Models;
using ModelAnalyses = RMC.BestFit.Analyses;

namespace RMC.BestFit.UI.Tests.Elements.UnivariateAnalysis;

/// <summary>
/// Verifies component-list edits own one undo record even before input data is selected.
/// </summary>
[TestClass]
public class MixtureDistributionUndoTests
{
    /// <summary>One component-list undo restores the complete configured model, while redo restores the edited model.</summary>
    /// <param name="operation">The component-list edit.</param>
    /// <remarks>Distinct parameter values and an informative prior expose losses hidden by default-only fixtures.</remarks>
    [STATestMethod]
    [DataRow("Remove")]
    [DataRow("Replace")]
    [DataRow("Add")]
    public void ConfiguredComponentEdit_OneUndoRestoresCompleteModelAndRedo(string operation)
    {
        var analysis = CreateConfiguredAnalysis();
        var input = analysis.InputData;
        var frame = input.DataFrame;
        var originalTypes = analysis.Distributions.ToArray();
        var originalModel = analysis.MixtureDistribution.ToXElement();
        Assert.IsTrue(analysis.IsEstimated, "Stored artifacts must establish the initial result state.");
        Assert.IsNotNull(analysis.AnalysisResults);
        Assert.IsNotNull(analysis.BayesianAnalysis.Results);
        analysis.UndoManager.Clear();

        switch (operation)
        {
            case "Remove": analysis.Distributions.RemoveAt(1); break;
            case "Replace": analysis.Distributions[1] = UnivariateDistributionType.Gumbel; break;
            case "Add": analysis.Distributions.Add(UnivariateDistributionType.Normal); break;
            default: throw new ArgumentException("Unknown component edit.", nameof(operation));
        }
        var editedTypes = analysis.Distributions.ToArray();
        var editedModel = analysis.MixtureDistribution.ToXElement();
        Assert.AreEqual(1, analysis.UndoManager.UndoStack.Count, "A component-list edit must own one complete undo action.");
        Assert.IsFalse(XNode.DeepEquals(originalModel, editedModel), "The edit must change the model.");
        AssertInvalidated(analysis, input, frame);

        analysis.UndoManager.Undo();

        CollectionAssert.AreEqual(originalTypes, analysis.Distributions.ToArray());
        Assert.IsTrue(XNode.DeepEquals(originalModel, analysis.MixtureDistribution.ToXElement()),
            "One Undo must restore all configured values, prior types and prior parameters, not merely the component enum list.");
        Assert.AreEqual(12345d, analysis.MixtureDistribution.Parameters[2].Value);
        Assert.AreEqual(UnivariateDistributionType.Normal, analysis.MixtureDistribution.Parameters[2].PriorDistribution.Type);
        Assert.IsFalse(analysis.UndoManager.CanUndo);
        Assert.IsTrue(analysis.UndoManager.CanRedo);
        AssertInvalidated(analysis, input, frame);

        analysis.UndoManager.Redo();

        CollectionAssert.AreEqual(editedTypes, analysis.Distributions.ToArray());
        Assert.IsTrue(XNode.DeepEquals(editedModel, analysis.MixtureDistribution.ToXElement()),
            "Redo must restore the complete post-edit model without recomputing a different configuration.");
        Assert.IsFalse(analysis.UndoManager.CanRedo);
        AssertInvalidated(analysis, input, frame);
    }

    /// <summary>Creates a valid configured mixture with an informative prior and injected stored results.</summary>
    /// <returns>The analysis ready for a component edit.</returns>
    /// <remarks>No sampler, optimizer, or numerical output calculation runs.</remarks>
    private static MixtureAnalysis CreateConfiguredAnalysis()
    {
        var analysis = CreateAnalysis();
        var frame = new DataFrame();
        for (int i = 0; i < 20; i++) frame.ExactSeries.Add(new ExactData(1990 + i, 10000d + 400d * i));
        analysis.InputData = new UI.InputData("Configured undo input", new InputDataCollection(BestFitProject.GetInstance()))
        {
            DataFrame = frame
        };
        var model = analysis.MixtureDistribution;
        model.UseDefaultFlatPriors = false;
        model.Parameters[2].Value = 12345d;
        model.Parameters[2].PriorDistribution = new Normal(12345d, 100d);
        var validation = model.Validate();
        Assert.IsTrue(validation.IsValid, string.Join(Environment.NewLine, validation.ValidationMessages));
        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        var stored = new MCMCResults(new ParameterSet((double[])parameters.Clone(), 0d),
            Enumerable.Range(0, 100).Select(_ => new ParameterSet((double[])parameters.Clone(), 0d)).ToList(), alpha: .10);
        analysis.BayesianAnalysis.OutputLength = 100;
        analysis.BayesianAnalysis.SetCustomMCMCResults(stored, skipInformationCriteria: true);
        var inner = (ModelAnalyses.MixtureAnalysis)analysis.InnerAnalysis;
        typeof(ModelAnalyses.AnalysisBase).GetField("_isEstimated", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(inner, true);
        typeof(ModelAnalyses.MixtureAnalysis).GetProperty(nameof(ModelAnalyses.MixtureAnalysis.AnalysisResults))!
            .SetValue(inner, new UncertaintyAnalysisResults
            {
                ParentDistribution = model.Mixture!.Clone(),
                ModeCurve = new double[inner.ProbabilityOrdinates.Count],
                MeanCurve = new double[inner.ProbabilityOrdinates.Count],
                ConfidenceIntervals = new double[inner.ProbabilityOrdinates.Count, 2]
            });
        return analysis;
    }

    /// <summary>Checks the existing structural-edit result invalidation policy and input identity.</summary>
    /// <param name="analysis">The edited analysis.</param>
    /// <param name="input">The original UI input element.</param>
    /// <param name="frame">The original model data frame.</param>
    private static void AssertInvalidated(MixtureAnalysis analysis, UI.InputData input, DataFrame frame)
    {
        Assert.AreSame(input, analysis.InputData);
        Assert.AreSame(frame, analysis.MixtureDistribution.DataFrame);
        Assert.IsFalse(analysis.IsEstimated);
        Assert.IsFalse(analysis.BayesianAnalysis.IsEstimated);
        Assert.IsNull(analysis.AnalysisResults);
        Assert.IsNull(analysis.BayesianAnalysis.Results);
    }

    /// <summary>
    /// Verifies deleting a default component does not leave an extra null-input model snapshot to undo.
    /// </summary>
    [STATestMethod]
    public void RemoveWithoutInputData_UndoRedoOwnsOneRecordAndPreservesNullInput()
    {
        var analysis = CreateAnalysis();
        analysis.Distributions.RemoveAt(1);

        Assert.AreEqual(1, analysis.UndoManager.UndoStack.Count);
        AssertCounts(analysis, 1);
        analysis.UndoManager.Undo();
        AssertCounts(analysis, 2);
        Assert.IsFalse(analysis.UndoManager.CanUndo);
        analysis.UndoManager.Undo();
        AssertCounts(analysis, 2);
        analysis.UndoManager.Redo();
        AssertCounts(analysis, 1);
        Assert.IsFalse(analysis.UndoManager.CanRedo);
        Assert.IsNull(analysis.InputData);
        Assert.IsNull(analysis.MixtureDistribution.DataFrame);
        Assert.IsFalse(analysis.IsEstimated);
    }

    /// <summary>
    /// Verifies an added component and a type edit each contribute exactly one reversible action.
    /// </summary>
    [STATestMethod]
    public void AddAndReplaceWithoutInputData_RestoreCompleteUndoRedoHistory()
    {
        var analysis = CreateAnalysis();
        analysis.Distributions.Add(UnivariateDistributionType.Normal);
        analysis.Distributions[1] = UnivariateDistributionType.Gumbel;
        Assert.AreEqual(2, analysis.UndoManager.UndoStack.Count);

        analysis.UndoManager.Undo();
        Assert.AreEqual(UnivariateDistributionType.Normal, analysis.Distributions[1]);
        analysis.UndoManager.Undo();
        AssertCounts(analysis, 2);
        Assert.IsFalse(analysis.UndoManager.CanUndo);
        analysis.UndoManager.Redo();
        AssertCounts(analysis, 3);
        analysis.UndoManager.Redo();
        Assert.AreEqual(UnivariateDistributionType.Gumbel, analysis.Distributions[1]);
        Assert.IsFalse(analysis.UndoManager.CanRedo);
        Assert.IsNull(analysis.MixtureDistribution.DataFrame);
    }

    /// <summary>
    /// Verifies collection synchronization preserves the caller's disabled-undo state.
    /// </summary>
    [STATestMethod]
    public void EditWithUndoDisabled_DoesNotEnableUndoOrCreateRecords()
    {
        var analysis = CreateAnalysis();
        analysis.IsUndoEnabled = false;
        analysis.Distributions.RemoveAt(1);
        AssertCounts(analysis, 1);
        Assert.IsFalse(analysis.IsUndoEnabled);
        Assert.IsFalse(analysis.UndoManager.CanUndo);
    }

    /// <summary>Verifies reset undo retains both the exact empty enum list and its configured model state.</summary>
    [STATestMethod]
    public void ResetAndSubsequentAdd_UndoRedoPreserveEmptyListAndModelSnapshots()
    {
        var analysis = CreateConfiguredAnalysis();
        XElement configured = analysis.MixtureDistribution.ToXElement();
        analysis.UndoManager.Clear();
        analysis.Distributions.Clear();
        Assert.AreEqual(0, analysis.Distributions.Count);
        Assert.AreEqual(1, analysis.UndoManager.UndoStack.Count);

        analysis.UndoManager.Undo();
        AssertCounts(analysis, 2);
        Assert.IsTrue(XNode.DeepEquals(configured, analysis.MixtureDistribution.ToXElement()));
        analysis.UndoManager.Redo();
        Assert.AreEqual(0, analysis.Distributions.Count);
        Assert.IsTrue(XNode.DeepEquals(configured, analysis.MixtureDistribution.ToXElement()));

        analysis.Distributions.Add(UnivariateDistributionType.Gumbel);
        analysis.UndoManager.Undo();
        Assert.AreEqual(0, analysis.Distributions.Count);
        Assert.IsTrue(XNode.DeepEquals(configured, analysis.MixtureDistribution.ToXElement()));
        analysis.UndoManager.Redo();
        AssertCounts(analysis, 1);
        Assert.AreEqual(UnivariateDistributionType.Gumbel, analysis.Distributions[0]);
    }

    /// <summary>Verifies edits made while undo is disabled still update the next action's complete baseline.</summary>
    [STATestMethod]
    public void DisabledEdit_FollowedByEnabledEdit_RestoresTheLatestConfiguredState()
    {
        var analysis = CreateConfiguredAnalysis();
        analysis.UndoManager.Clear();
        analysis.IsUndoEnabled = false;
        analysis.Distributions.Add(UnivariateDistributionType.Normal);
        Assert.IsFalse(analysis.UndoManager.CanUndo);
        analysis.MixtureDistribution.Parameters[3].Value = 12345d;
        analysis.MixtureDistribution.Parameters[3].PriorDistribution = new Normal(12345d, 100d);
        XElement configured = analysis.MixtureDistribution.ToXElement();

        analysis.IsUndoEnabled = true;
        analysis.Distributions.RemoveAt(2);
        Assert.AreEqual(1, analysis.UndoManager.UndoStack.Count);
        analysis.UndoManager.Undo();
        AssertCounts(analysis, 3);
        Assert.IsTrue(XNode.DeepEquals(configured, analysis.MixtureDistribution.ToXElement()));
        Assert.IsTrue(analysis.IsUndoEnabled);
    }

    /// <summary>
    /// Creates a fresh unestimated mixture outside persistent project storage.
    /// </summary>
    /// <returns>The two-component analysis with an empty undo history.</returns>
    private static MixtureAnalysis CreateAnalysis()
    {
        var collection = new UnivariateAnalysisCollection(BestFitProject.GetInstance());
        return new MixtureAnalysis("UndoProbe", collection);
    }

    /// <summary>
    /// Verifies that the public component list and the model remain synchronized.
    /// </summary>
    /// <param name="analysis">The analysis under test.</param>
    /// <param name="expected">The expected component count.</param>
    private static void AssertCounts(MixtureAnalysis analysis, int expected)
    {
        Assert.AreEqual(expected, analysis.Distributions.Count);
        Assert.AreEqual(expected, analysis.MixtureDistribution.Mixture!.Distributions.Length);
    }
}
