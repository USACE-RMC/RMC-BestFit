using FrameworkInterfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.Elements.InputData;

/// <summary>
/// Unit tests for <see cref="InputDataCollection"/>, the project-level container of
/// <see cref="InputData"/> instances.
/// </summary>
/// <remarks>
/// Tests focus on: collection name constant, constructor, and IElementCollection contract.
/// SQLite-dependent methods (Save, Open, Delete, Add, Insert) are excluded — covered by integration tests.
/// </remarks>
[TestClass]
public class InputDataCollectionTests
{
    /// <summary>
    /// Verifies that <see cref="InputDataCollection.Name"/> returns the expected constant string.
    /// </summary>
    [TestMethod]
    public void Name_ReturnsExpectedConstant()
    {
        var project = BestFitProject.GetInstance();
        var collection = new InputDataCollection(project);

        Assert.AreEqual("Input Data", collection.Name);
    }

    /// <summary>
    /// Verifies that a freshly created collection is not dirty.
    /// </summary>
    [TestMethod]
    public void NewCollection_IsNotDirty()
    {
        var project = BestFitProject.GetInstance();
        var collection = new InputDataCollection(project);

        Assert.IsFalse(collection.IsDirty);
    }

    /// <summary>
    /// Verifies that <see cref="InputDataCollection"/> implements <see cref="IElementCollection"/>.
    /// </summary>
    [TestMethod]
    public void ImplementsIElementCollection()
    {
        var project = BestFitProject.GetInstance();
        var collection = new InputDataCollection(project);

        Assert.IsInstanceOfType<IElementCollection>(collection);
    }

    /// <summary>
    /// Verifies that <see cref="IElementCollection.ParentProject"/> is set to the provided project.
    /// </summary>
    [TestMethod]
    public void ParentProject_IsSetCorrectly()
    {
        var project = BestFitProject.GetInstance();
        var collection = new InputDataCollection(project);

        Assert.AreEqual(project, collection.ParentProject);
    }

}
