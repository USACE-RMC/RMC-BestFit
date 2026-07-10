using FrameworkInterfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.Elements.TimeSeriesAnalysis;

/// <summary>
/// Unit tests for <see cref="TimeSeriesAnalysisCollection"/>.
/// </summary>
[TestClass]
public class TimeSeriesAnalysisCollectionTests
{
    /// <summary>
    /// Verifies that <see cref="TimeSeriesAnalysisCollection.Name"/> returns the expected constant string.
    /// </summary>
    [TestMethod]
    public void Name_ReturnsExpectedConstant()
    {
        var project = BestFitProject.GetInstance();
        var collection = new TimeSeriesAnalysisCollection(project);

        Assert.AreEqual("Time Series Analysis", collection.Name);
    }

    /// <summary>
    /// Verifies that <see cref="TimeSeriesAnalysisCollection"/> implements <see cref="IElementCollection"/>.
    /// </summary>
    [TestMethod]
    public void ImplementsIElementCollection()
    {
        var project = BestFitProject.GetInstance();
        var collection = new TimeSeriesAnalysisCollection(project);

        Assert.IsInstanceOfType<IElementCollection>(collection);
    }

    /// <summary>
    /// Verifies that <see cref="IElementCollection.ParentProject"/> is set correctly.
    /// </summary>
    [TestMethod]
    public void ParentProject_IsSetCorrectly()
    {
        var project = BestFitProject.GetInstance();
        var collection = new TimeSeriesAnalysisCollection(project);

        Assert.AreEqual(project, collection.ParentProject);
    }

    /// <summary>
    /// Verifies that a freshly constructed collection is not dirty.
    /// </summary>
    [TestMethod]
    public void NewCollection_IsNotDirty()
    {
        var collection = new TimeSeriesAnalysisCollection(BestFitProject.GetInstance());
        Assert.IsFalse(collection.IsDirty);
    }

}
