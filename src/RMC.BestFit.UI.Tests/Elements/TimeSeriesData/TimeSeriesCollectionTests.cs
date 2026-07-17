using FrameworkInterfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.Elements.TimeSeriesData;

/// <summary>
/// Unit tests for <see cref="TimeSeriesCollection"/>, the project-level container of
/// <see cref="TimeSeriesElement"/> instances.
/// </summary>
/// <remarks>
/// Tests focus on: collection name constant, constructor, IsDirty initial state, and Count.
/// SQLite-dependent methods (Save, Open, Delete) are excluded — covered by integration tests.
/// </remarks>
[TestClass]
public class TimeSeriesCollectionTests
{
    /// <summary>
    /// Verifies that <see cref="TimeSeriesCollection.Name"/> returns the expected constant string.
    /// </summary>
    [TestMethod]
    public void Name_ReturnsExpectedConstant()
    {
        var project = BestFitProject.GetInstance();
        var collection = new TimeSeriesCollection(project);

        Assert.AreEqual("Time Series Data", collection.Name);
    }

    /// <summary>
    /// Verifies that a freshly created collection is not dirty.
    /// </summary>
    [TestMethod]
    public void NewCollection_IsNotDirty()
    {
        var project = BestFitProject.GetInstance();
        var collection = new TimeSeriesCollection(project);

        // ElementCollectionBase sets IsDirty = false in base constructor
        Assert.IsFalse(collection.IsDirty);
    }

    /// <summary>
    /// Verifies that <see cref="TimeSeriesCollection"/> inherits from <see cref="IElementCollection"/>.
    /// </summary>
    [TestMethod]
    public void ImplementsIElementCollection()
    {
        var project = BestFitProject.GetInstance();
        var collection = new TimeSeriesCollection(project);

        Assert.IsInstanceOfType<IElementCollection>(collection);
    }

    /// <summary>
    /// Verifies that <see cref="IElementCollection.ParentProject"/> is set to the provided project.
    /// </summary>
    [TestMethod]
    public void ParentProject_IsSetCorrectly()
    {
        var project = BestFitProject.GetInstance();
        var collection = new TimeSeriesCollection(project);

        Assert.AreEqual(project, collection.ParentProject);
    }

}
