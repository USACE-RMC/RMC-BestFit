using FrameworkInterfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.Elements.DistributionFitting;

/// <summary>
/// Unit tests for <see cref="FittingAnalysisCollection"/>, the project-level container of
/// <see cref="FittingAnalysis"/> instances.
/// </summary>
/// <remarks>
/// Tests focus on the collection name constant and the IElementCollection contract.
/// SQLite-dependent methods are excluded — covered by integration tests.
/// </remarks>
[TestClass]
public class FittingAnalysisCollectionTests
{
    /// <summary>
    /// Verifies that <see cref="FittingAnalysisCollection.Name"/> returns the expected constant string.
    /// </summary>
    [TestMethod]
    public void Name_ReturnsExpectedConstant()
    {
        var project = BestFitProject.GetInstance();
        var collection = new FittingAnalysisCollection(project);

        Assert.AreEqual("Distribution Fitting Analysis", collection.Name);
    }

    /// <summary>
    /// Verifies that <see cref="FittingAnalysisCollection"/> implements <see cref="IElementCollection"/>.
    /// </summary>
    [TestMethod]
    public void ImplementsIElementCollection()
    {
        var project = BestFitProject.GetInstance();
        var collection = new FittingAnalysisCollection(project);

        Assert.IsInstanceOfType<IElementCollection>(collection);
    }

    /// <summary>
    /// Verifies that <see cref="IElementCollection.ParentProject"/> is set correctly.
    /// </summary>
    [TestMethod]
    public void ParentProject_IsSetCorrectly()
    {
        var project = BestFitProject.GetInstance();
        var collection = new FittingAnalysisCollection(project);

        Assert.AreEqual(project, collection.ParentProject);
    }

    /// <summary>
    /// Verifies that a freshly constructed collection is not dirty.
    /// </summary>
    [TestMethod]
    public void NewCollection_IsNotDirty()
    {
        var collection = new FittingAnalysisCollection(BestFitProject.GetInstance());
        Assert.IsFalse(collection.IsDirty);
    }

    /// <summary>
    /// Verifies that <see cref="FittingAnalysisCollection.InsertFromExternalProject"/> is a
    /// no-op (FittingAnalysis cannot be copied from external projects). The implementation
    /// always returns without modifying state, regardless of inputs.
    /// </summary>
    [TestMethod]
    public void InsertFromExternalProject_AnyType_NoThrow()
    {
        var collection = new FittingAnalysisCollection(BestFitProject.GetInstance());

        collection.InsertFromExternalProject(0, "FittingAnalysis", nameof(FittingAnalysis), string.Empty);
        collection.InsertFromExternalProject(0, "anything", "Some.Other.Type", string.Empty);
    }
}
