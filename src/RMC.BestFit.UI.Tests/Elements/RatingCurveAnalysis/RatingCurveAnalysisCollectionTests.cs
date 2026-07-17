using FrameworkInterfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.Elements.RatingCurveAnalysis;

/// <summary>
/// Unit tests for <see cref="RatingCurveAnalysisCollection"/>.
/// </summary>
[TestClass]
public class RatingCurveAnalysisCollectionTests
{
    /// <summary>
    /// Verifies that <see cref="RatingCurveAnalysisCollection.Name"/> returns the expected constant string.
    /// </summary>
    [TestMethod]
    public void Name_ReturnsExpectedConstant()
    {
        var project = BestFitProject.GetInstance();
        var collection = new RatingCurveAnalysisCollection(project);

        Assert.AreEqual("Rating Curve Analysis", collection.Name);
    }

    /// <summary>
    /// Verifies that <see cref="RatingCurveAnalysisCollection"/> implements <see cref="IElementCollection"/>.
    /// </summary>
    [TestMethod]
    public void ImplementsIElementCollection()
    {
        var project = BestFitProject.GetInstance();
        var collection = new RatingCurveAnalysisCollection(project);

        Assert.IsInstanceOfType<IElementCollection>(collection);
    }

    /// <summary>
    /// Verifies that <see cref="IElementCollection.ParentProject"/> is set correctly.
    /// </summary>
    [TestMethod]
    public void ParentProject_IsSetCorrectly()
    {
        var project = BestFitProject.GetInstance();
        var collection = new RatingCurveAnalysisCollection(project);

        Assert.AreEqual(project, collection.ParentProject);
    }

    /// <summary>
    /// Verifies that a freshly constructed collection is not dirty.
    /// </summary>
    [TestMethod]
    public void NewCollection_IsNotDirty()
    {
        var collection = new RatingCurveAnalysisCollection(BestFitProject.GetInstance());
        Assert.IsFalse(collection.IsDirty);
    }

}
