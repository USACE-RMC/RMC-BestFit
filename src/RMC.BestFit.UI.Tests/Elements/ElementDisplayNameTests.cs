using System.ComponentModel;
using FrameworkInterfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.Elements;

/// <summary>Protects document caption notifications from real element rename undo and redo.</summary>
/// <remarks>Exercises private projects and real serializers without running any estimator.</remarks>
[TestClass]
[DoNotParallelize]
public class ElementDisplayNameTests
{
    /// <summary>Combines every persisted element type with dirty and saved rename replay.</summary>
    /// <returns>Element kind and whether the renamed element is saved before replay.</returns>
    public static IEnumerable<object[]> RenameCases()
    {
        foreach (string kind in new[] { "InputData", "TimeSeriesElement", "FittingAnalysis", "UnivariateAnalysis",
            "B17CAnalysis", "MixtureAnalysis", "PointProcessAnalysis", "CompositeAnalysis", "BivariateAnalysis",
            "CoincidentFrequencyAnalysis", "RatingCurveAnalysis", "TimeSeriesAnalysis" })
            foreach (bool saveAfterRename in new[] { false, true })
                yield return new object[] { kind, saveAfterRename };
    }

    /// <summary>Rename replay refreshes the caption while retaining the established dirty-state policy.</summary>
    /// <param name="kind">The actual element implementation to exercise.</param>
    /// <param name="saveAfterRename">Whether replay starts from a clean saved rename.</param>
    [STATestMethod]
    [DynamicData(nameof(RenameCases), DynamicDataSourceType.Method)]
    public void RenameUndoRedo_RefreshesDisplayNameWithoutChangingDirtyPolicy(string kind, bool saveAfterRename)
    {
        using var scope = new SQLitePersistenceTestScope();
        ElementBase element = CreateElement(kind, scope);
        ((ElementCollectionBase)element.ParentCollection).Add(element);
        element.Save();
        element.UndoManager.Clear();
        Assert.IsFalse(element.IsDirty);
        Assert.AreEqual("Original", element.DisplayName);

        var changes = new List<string?>();
        PropertyChangedEventHandler observer = (_, args) => changes.Add(args.PropertyName);
        element.PropertyChanged += observer;
        try
        {
            element.Name = "Renamed";
            Assert.IsTrue(element.IsDirty);
            Assert.AreEqual("Renamed*", element.DisplayName);
            CollectionAssert.Contains(changes, nameof(IElement.Name));
            CollectionAssert.Contains(changes, nameof(IElement.DisplayName));
            Assert.IsTrue(element.UndoManager.CanUndo);

            if (saveAfterRename)
            {
                element.Save();
                Assert.IsFalse(element.IsDirty);
                Assert.AreEqual("Renamed", element.DisplayName);
            }

            bool expectedDirty = !saveAfterRename;
            DateTime savedTimestamp = element.LastModified;
            string persistedName = element.NameOnDisk;
            changes.Clear();
            element.UndoManager.Undo();
            Assert.AreEqual("Original", element.Name);
            Assert.AreEqual(expectedDirty, element.IsDirty, "Caption refresh must not alter undo dirty-state policy.");
            Assert.AreEqual(savedTimestamp, element.LastModified);
            Assert.AreEqual(persistedName, element.NameOnDisk);
            Assert.AreEqual(expectedDirty ? "Original*" : "Original", element.DisplayName);
            CollectionAssert.Contains(changes, nameof(IElement.Name));
            CollectionAssert.Contains(changes, nameof(IElement.DisplayName));
            CollectionAssert.DoesNotContain(changes, nameof(IElement.IsDirty));
            Assert.IsTrue(element.UndoManager.CanRedo);

            changes.Clear();
            element.UndoManager.Redo();
            Assert.AreEqual("Renamed", element.Name);
            Assert.AreEqual(expectedDirty, element.IsDirty);
            Assert.AreEqual(savedTimestamp, element.LastModified);
            Assert.AreEqual(persistedName, element.NameOnDisk);
            Assert.AreEqual(expectedDirty ? "Renamed*" : "Renamed", element.DisplayName);
            CollectionAssert.Contains(changes, nameof(IElement.Name));
            CollectionAssert.Contains(changes, nameof(IElement.DisplayName));
            CollectionAssert.DoesNotContain(changes, nameof(IElement.IsDirty));
            Assert.IsTrue(element.UndoManager.CanUndo);
            Assert.IsFalse(element.UndoManager.CanRedo);
            scope.AssertReleased();
        }
        finally
        {
            element.PropertyChanged -= observer;
        }
    }

    /// <summary>Constructs a real element within its test-owned production collection.</summary>
    /// <param name="kind">The element implementation name.</param>
    /// <param name="scope">The private project and file owner.</param>
    /// <returns>The requested actual element implementation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The element kind is unknown.</exception>
    private static ElementBase CreateElement(string kind, SQLitePersistenceTestScope scope) => kind switch
    {
        "InputData" => new UI.InputData("Original", Collection<InputDataCollection>(scope)),
        "TimeSeriesElement" => new TimeSeriesElement("Original", Collection<TimeSeriesCollection>(scope)),
        "FittingAnalysis" => new FittingAnalysis("Original", Collection<FittingAnalysisCollection>(scope)),
        "UnivariateAnalysis" => new UI.UnivariateAnalysis("Original", Collection<UnivariateAnalysisCollection>(scope)),
        "B17CAnalysis" => new B17CAnalysis("Original", Collection<UnivariateAnalysisCollection>(scope)),
        "MixtureAnalysis" => new MixtureAnalysis("Original", Collection<UnivariateAnalysisCollection>(scope)),
        "PointProcessAnalysis" => new PointProcessAnalysis("Original", Collection<UnivariateAnalysisCollection>(scope)),
        "CompositeAnalysis" => new CompositeAnalysis("Original", Collection<UnivariateAnalysisCollection>(scope)),
        "BivariateAnalysis" => new UI.BivariateAnalysis("Original", Collection<BivariateAnalysisCollection>(scope)),
        "CoincidentFrequencyAnalysis" => new CoincidentFrequencyAnalysis("Original", Collection<BivariateAnalysisCollection>(scope)),
        "RatingCurveAnalysis" => new UI.RatingCurveAnalysis("Original", Collection<RatingCurveAnalysisCollection>(scope)),
        "TimeSeriesAnalysis" => new UI.TimeSeriesAnalysis("Original", Collection<TimeSeriesAnalysisCollection>(scope)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    /// <summary>Resolves the element's production collection from the private project.</summary>
    /// <typeparam name="T">The collection type.</typeparam>
    /// <param name="scope">The private project owner.</param>
    /// <returns>The unique matching collection.</returns>
    private static T Collection<T>(SQLitePersistenceTestScope scope) => scope.Collections.OfType<T>().Single();
}
