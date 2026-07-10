using FrameworkInterfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.Elements.BivariateAnalysis;

/// <summary>
/// Unit tests for <see cref="BivariateAnalysisCollection"/>, the project-level container of
/// <see cref="BivariateAnalysis"/> instances.
/// </summary>
/// <remarks>
/// Tests focus on the collection name constant and the IElementCollection contract.
/// SQLite-dependent methods are excluded — covered by integration tests.
/// </remarks>
[TestClass]
public class BivariateAnalysisCollectionTests
{
    /// <summary>
    /// Verifies that <see cref="BivariateAnalysisCollection.Name"/> returns the expected constant string.
    /// </summary>
    [TestMethod]
    public void Name_ReturnsExpectedConstant()
    {
        var project = BestFitProject.GetInstance();
        var collection = new BivariateAnalysisCollection(project);

        Assert.AreEqual("Bivariate Distribution Analysis", collection.Name);
    }

    /// <summary>
    /// Verifies that <see cref="BivariateAnalysisCollection"/> implements <see cref="IElementCollection"/>.
    /// </summary>
    [TestMethod]
    public void ImplementsIElementCollection()
    {
        var project = BestFitProject.GetInstance();
        var collection = new BivariateAnalysisCollection(project);

        Assert.IsInstanceOfType<IElementCollection>(collection);
    }

    /// <summary>
    /// Verifies that <see cref="IElementCollection.ParentProject"/> is set correctly.
    /// </summary>
    [TestMethod]
    public void ParentProject_IsSetCorrectly()
    {
        var project = BestFitProject.GetInstance();
        var collection = new BivariateAnalysisCollection(project);

        Assert.AreEqual(project, collection.ParentProject);
    }

    /// <summary>
    /// Verifies that a freshly constructed collection is not dirty.
    /// </summary>
    [TestMethod]
    public void NewCollection_IsNotDirty()
    {
        var collection = new BivariateAnalysisCollection(BestFitProject.GetInstance());
        Assert.IsFalse(collection.IsDirty);
    }

}
