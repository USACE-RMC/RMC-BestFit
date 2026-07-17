using System.ComponentModel;
using System.Reflection;
using FrameworkInterfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.Elements.InputData;

/// <summary>
/// Tests the dirty-state propagation chain from element → collection → project.
/// </summary>
/// <remarks>
/// Invariants under test:
/// <list type="bullet">
/// <item><description>Element <see cref="ISave.IsDirty"/> <c>false → true</c> transitions
///   fire <see cref="IElementCollection.ElementIsDirtyChanged"/> on the owning collection.</description></item>
/// <item><description>Element <see cref="ISave.IsDirty"/> <c>true → false</c> transitions
///   (what <see cref="ISave.Save"/> does) do NOT fire the event — each level handles its
///   own clean state, and propagating it upward would re-dirty levels that just cleared themselves.</description></item>
/// <item><description>Collection structural changes (Add / Remove / Move / Sort) flip
///   <see cref="ISave.IsDirty"/> on the collection itself, which fires
///   <see cref="INotifyPropertyChanged.PropertyChanged"/>(nameof(IsDirty)). This is the
///   OTHER leg of propagation; the project aggregates that too.</description></item>
/// </list>
/// The canonical <see cref="UI.InputData"/> / <see cref="UI.InputDataCollection"/>
/// pair is used; the behavior is implemented in <see cref="ElementCollectionBase"/>
/// so it applies uniformly to all six other BestFit collections.
/// </remarks>
[TestClass]
public class InputDataCollectionDirtyPropagationTests
{
    /// <summary>
    /// Builds a fresh test-local <see cref="InputDataCollection"/>. Each test gets
    /// its own collection so parallel test execution (see
    /// <c>MSTestSettings.cs</c> — <c>Parallelize</c>, scope <c>MethodLevel</c>) does
    /// not let one test's PropertyChanged / ElementIsDirtyChanged events leak into
    /// another's observed counts.
    /// </summary>
    private static InputDataCollection NewCollection() =>
        new InputDataCollection(BestFitProject.GetInstance());

    /// <summary>
    /// An element going <see cref="ISave.IsDirty"/> <c>false → true</c> must raise
    /// <see cref="IElementCollection.ElementIsDirtyChanged"/> on the owning collection.
    /// This is the bridge that lets the project aggregate element-dirty state without
    /// forcing collections to flip their own <see cref="ISave.IsDirty"/>.
    /// </summary>
    [STATestMethod]
    public void ElementIsDirty_FalseToTrue_RaisesElementIsDirtyChanged()
    {
        var collection = NewCollection();
        var element = new UI.InputData("DirtyBridge-FalseToTrue", collection);
        AddWithoutDiskSave(collection, element);
        ForceClean(element);

        int fireCount = 0;
        EventHandler handler = (_, _) => fireCount++;
        collection.ElementIsDirtyChanged += handler;
        try
        {
            // Touching a RecordPropertyChange-gated property is a user edit → IsDirty=true.
            element.UnitLabel = "cfs";

            Assert.IsTrue(element.IsDirty, "Precondition: edit should mark element dirty.");
            Assert.AreEqual(1, fireCount, "ElementIsDirtyChanged must fire exactly once on the false→true transition.");
        }
        finally
        {
            collection.ElementIsDirtyChanged -= handler;
            collection.Remove(element);
        }
    }

    /// <summary>
    /// A subsequent edit while the element is already dirty must NOT raise
    /// <see cref="IElementCollection.ElementIsDirtyChanged"/> again — the handler keys
    /// on the transition, not on every PropertyChanged. Otherwise the project would
    /// be told the child is "newly dirty" on every keystroke.
    /// </summary>
    [STATestMethod]
    public void ElementIsDirty_AlreadyTrue_DoesNotRaiseAgain()
    {
        var collection = NewCollection();
        var element = new UI.InputData("DirtyBridge-AlreadyTrue", collection);
        AddWithoutDiskSave(collection, element);
        ForceClean(element);

        // First edit — should raise once.
        element.UnitLabel = "cfs";
        Assert.IsTrue(element.IsDirty);

        int fireCount = 0;
        EventHandler handler = (_, _) => fireCount++;
        collection.ElementIsDirtyChanged += handler;
        try
        {
            // Second edit while already dirty — SetIsDirty(true) is a no-op (value
            // unchanged) so PropertyChanged(IsDirty) does not fire, and our handler
            // never sees a false→true transition.
            element.UnitLabel = "m^3/s";

            Assert.AreEqual(0, fireCount, "ElementIsDirtyChanged must not fire for true→true (already dirty).");
        }
        finally
        {
            collection.ElementIsDirtyChanged -= handler;
            collection.Remove(element);
        }
    }

    /// <summary>
    /// The <c>IsDirty</c> <c>true → false</c> transition (as done at the end of
    /// <see cref="ISave.Save"/>) must NOT raise
    /// <see cref="IElementCollection.ElementIsDirtyChanged"/>. Propagating clean
    /// transitions upward would cause the project to immediately re-dirty itself
    /// right after finishing a save.
    /// </summary>
    [STATestMethod]
    public void ElementIsDirty_TrueToFalse_DoesNotRaise()
    {
        var collection = NewCollection();
        var element = new UI.InputData("DirtyBridge-TrueToFalse", collection);
        AddWithoutDiskSave(collection, element);
        // Make it dirty first.
        element.UnitLabel = "cfs";
        Assert.IsTrue(element.IsDirty);

        int fireCount = 0;
        EventHandler handler = (_, _) => fireCount++;
        collection.ElementIsDirtyChanged += handler;
        try
        {
            // Simulate what Save() does: clear IsDirty via the protected setter.
            ForceClean(element);

            Assert.IsFalse(element.IsDirty);
            Assert.AreEqual(0, fireCount, "ElementIsDirtyChanged must not fire on true→false (clean-up) transitions.");
        }
        finally
        {
            collection.ElementIsDirtyChanged -= handler;
            collection.Remove(element);
        }
    }

    /// <summary>
    /// Structural changes to a collection (Add / Remove / Move / Sort) route
    /// through the base-class <c>SetIsDirty(true)</c>, which fires
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> with
    /// <c>nameof(IsDirty)</c>. This test pins the contract that
    /// <c>BestFitProject.OnCollectionPropertyChanged</c> relies on: the collection
    /// publishes <c>IsDirty</c> transitions via PropertyChanged, and does so only
    /// on the <c>false → true</c> edge (<c>SetIsDirty</c> short-circuits when
    /// <c>IsDirty</c> is unchanged).
    /// </summary>
    [STATestMethod]
    public void CollectionSetIsDirty_TrueTransition_RaisesPropertyChangedIsDirty()
    {
        var collection = new InputDataCollection(BestFitProject.GetInstance());
        ForceCleanCollection(collection);

        var dirtyEvents = new List<string>();
        collection.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ISave.IsDirty))
                dirtyEvents.Add(e.PropertyName);
        };

        // Simulate what Add / Remove / Move / Sort do under the hood.
        InvokeSetIsDirty(collection, true);
        // Second call is a no-op (already true) — must not fire again.
        InvokeSetIsDirty(collection, true);

        Assert.IsTrue(collection.IsDirty);
        Assert.AreEqual(1, dirtyEvents.Count,
            "PropertyChanged(nameof(IsDirty)) must fire only on the false→true transition, not repeated true assignments.");
    }

    /// <summary>
    /// The collection's own <see cref="ISave.IsDirty"/> must NOT be flipped by a
    /// child element becoming dirty. That's the whole point of routing element
    /// dirty through <see cref="IElementCollection.ElementIsDirtyChanged"/> instead
    /// of through <see cref="ISave.IsDirty"/>: double-counting at the collection
    /// level would make it impossible to tell "the collection changed" apart from
    /// "a child changed".
    /// </summary>
    [STATestMethod]
    public void ChildIsDirty_DoesNotFlipCollectionIsDirty()
    {
        var collection = new InputDataCollection(BestFitProject.GetInstance());
        var element = new UI.InputData("NoDoubleCounting", collection);
        AddWithoutDiskSave(collection, element);
        ForceClean(element);
        ForceCleanCollection(collection);
        Assert.IsFalse(collection.IsDirty, "Baseline: collection must start clean for this test.");

        element.UnitLabel = "cfs";

        try
        {
            Assert.IsTrue(element.IsDirty);
            Assert.IsFalse(collection.IsDirty,
                "Collection.IsDirty is structural-only and must NOT flip merely because a child became dirty.");
        }
        finally
        {
            collection.Remove(element);
        }
    }

    /// <summary>
    /// Property change events on an element other than <c>Name</c> or <c>IsDirty</c>
    /// (e.g., the element's own <c>UnitLabel</c> PropertyChanged) must NOT raise
    /// <see cref="IElementCollection.ElementIsDirtyChanged"/>. The event is
    /// specifically the dirty-transition bridge; other property changes are noise
    /// from the collection's point of view.
    /// </summary>
    [STATestMethod]
    public void ElementNonDirtyPropertyChange_DoesNotRaiseElementIsDirtyChanged()
    {
        var collection = NewCollection();
        var element = new UI.InputData("UnrelatedPropertyChange", collection);
        AddWithoutDiskSave(collection, element);
        // Make it dirty first so subsequent edits do not also trigger the dirty transition.
        element.UnitLabel = "first";

        int fireCount = 0;
        EventHandler handler = (_, _) => fireCount++;
        collection.ElementIsDirtyChanged += handler;
        try
        {
            // Another UnitLabel edit fires PropertyChanged("UnitLabel") but NOT
            // PropertyChanged("IsDirty") (it was already true). Handler should
            // ignore the UnitLabel notification entirely.
            element.UnitLabel = "second";

            Assert.AreEqual(0, fireCount,
                "Only IsDirty transitions should raise ElementIsDirtyChanged — not other property changes.");
        }
        finally
        {
            collection.ElementIsDirtyChanged -= handler;
            collection.Remove(element);
        }
    }

    // --- Helpers ---------------------------------------------------------

    /// <summary>
    /// Adds an element to the collection without triggering the SQLite write that
    /// <c>InputDataCollection.Add</c> normally performs. Flips the protected
    /// <c>_opening</c> flag on the collection so the Add path short-circuits the
    /// disk save — there is no backing <c>.bestfit</c> file in these unit tests.
    /// The PropertyChanged subscription wiring still runs, which is what the
    /// propagation tests rely on.
    /// </summary>
    private static void AddWithoutDiskSave(InputDataCollection collection, UI.InputData element)
    {
        var openingField = FindInstanceField(collection.GetType(), "_opening");
        object? prior = openingField.GetValue(collection);
        openingField.SetValue(collection, true);
        try
        {
            collection.Add(element);
        }
        finally
        {
            openingField.SetValue(collection, prior);
        }
    }

    /// <summary>
    /// Clears <see cref="ISave.IsDirty"/> on an element by invoking the protected
    /// <c>SetIsDirty(false)</c> via reflection. Used to reset state between steps
    /// of a test without involving a full SQLite Save cycle.
    /// </summary>
    private static void ForceClean(UI.InputData element)
    {
        var method = FindSetIsDirty(element.GetType());
        method.Invoke(element, new object[] { false });
    }

    /// <summary>
    /// Clears <see cref="ISave.IsDirty"/> on a collection via reflection.
    /// </summary>
    private static void ForceCleanCollection(InputDataCollection collection)
    {
        var method = FindSetIsDirty(collection.GetType());
        method.Invoke(collection, new object[] { false });
    }

    /// <summary>
    /// Walks the inheritance chain to find the protected instance
    /// <c>SetIsDirty(bool)</c> method. The method is defined on the base class
    /// but reflection on a derived type does not see it by default.
    /// </summary>
    private static MethodInfo FindSetIsDirty(Type type)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            var m = t.GetMethod("SetIsDirty",
                BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                types: new[] { typeof(bool) },
                modifiers: null);
            if (m != null) return m;
        }
        throw new InvalidOperationException($"SetIsDirty(bool) not found on {type.FullName} or any base.");
    }

    /// <summary>
    /// Walks the inheritance chain to find a non-public instance field by name.
    /// </summary>
    private static FieldInfo FindInstanceField(Type type, string name)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            var f = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) return f;
        }
        throw new InvalidOperationException($"Field '{name}' not found on {type.FullName} or any base.");
    }

    /// <summary>
    /// Invokes the collection's protected <c>SetIsDirty(bool)</c> to simulate what
    /// structural-change paths (Add / Remove / Move / Sort) do internally.
    /// </summary>
    private static void InvokeSetIsDirty(InputDataCollection collection, bool value)
    {
        var method = FindSetIsDirty(collection.GetType());
        method.Invoke(collection, new object[] { value });
    }
}
