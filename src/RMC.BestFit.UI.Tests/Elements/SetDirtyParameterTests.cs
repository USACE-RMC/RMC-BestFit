using System.ComponentModel;
using System.Reflection;
using FrameworkInterfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.Elements;

/// <summary>
/// Pins the semantics of the <c>setDirty</c> parameter on
/// <c>RaisePropertyChange</c> / <c>RecordPropertyChange</c>:
/// <list type="bullet">
/// <item><description><c>setDirty: true</c> — promotes the entity to dirty
///   (sets <c>IsDirty=true</c>).</description></item>
/// <item><description><c>setDirty: false</c> — does NOT modify <c>IsDirty</c>.
///   Only raises PropertyChanged for UI bindings. Clearing dirty is the
///   explicit responsibility of <c>SetIsDirty(false)</c> from Save/Open/Copy tails.</description></item>
/// </list>
/// These tests exist because the previous implementation called
/// <c>SetIsDirty(isDirty)</c> unconditionally, which wrongly force-cleared
/// the flag when callers passed <c>false</c> expecting a notify-only behavior.
/// The layout setters (<c>AvalonDockLayout</c>, <c>ProjectExplorerLayout</c>)
/// relied on that notify-only behavior; their test cases appear below as
/// concrete end-to-end regressions.
/// </summary>
[TestClass]
[DoNotParallelize]
public class SetDirtyParameterTests
{
    /// <summary>
    /// On an element that is currently dirty, a <c>RaisePropertyChange(..., setDirty: false)</c>
    /// call must preserve <c>IsDirty=true</c>. Regression for the bug where the Save button
    /// left child asterisks visible because a preceding layout setter silently cleared
    /// <c>Project.IsDirty</c>.
    /// </summary>
    [STATestMethod]
    public void RaisePropertyChange_SetDirtyFalse_PreservesExistingDirty()
    {
        var collection = new InputDataCollection(BestFitProject.GetInstance());
        var element = new UI.InputData("PreserveDirty-Raise", collection);
        ForceSetIsDirty(element, true);
        Assert.IsTrue(element.IsDirty, "Precondition: element should start dirty.");

        InvokeRaisePropertyChange(element, nameof(UI.InputData.UnitLabel), setDirty: false);

        Assert.IsTrue(element.IsDirty,
            "RaisePropertyChange(name, setDirty: false) must NOT clear IsDirty. " +
            "Clearing is the explicit responsibility of SetIsDirty(false).");
    }

    /// <summary>
    /// <c>setDirty: false</c> must still fire the <c>PropertyChanged</c> event — the
    /// call is a notify-only mechanism, it just doesn't touch the dirty flag.
    /// </summary>
    [STATestMethod]
    public void RaisePropertyChange_SetDirtyFalse_StillFiresPropertyChanged()
    {
        var collection = new InputDataCollection(BestFitProject.GetInstance());
        var element = new UI.InputData("NotifyOnly-Raise", collection);

        var propertyNames = new List<string>();
        PropertyChangedEventHandler handler = (_, e) => propertyNames.Add(e.PropertyName ?? "");
        element.PropertyChanged += handler;
        try
        {
            InvokeRaisePropertyChange(element, nameof(UI.InputData.Description), setDirty: false);
        }
        finally { element.PropertyChanged -= handler; }

        CollectionAssert.Contains(propertyNames, nameof(UI.InputData.Description),
            "RaisePropertyChange must always fire PropertyChanged regardless of setDirty.");
    }

    /// <summary>
    /// On a clean element, <c>RaisePropertyChange(..., setDirty: true)</c> must promote
    /// it to dirty — this is the common property-setter path.
    /// </summary>
    [STATestMethod]
    public void RaisePropertyChange_SetDirtyTrue_PromotesToDirty()
    {
        var collection = new InputDataCollection(BestFitProject.GetInstance());
        var element = new UI.InputData("PromoteDirty-Raise", collection);
        ForceSetIsDirty(element, false);
        Assert.IsFalse(element.IsDirty, "Precondition: element should start clean.");

        InvokeRaisePropertyChange(element, nameof(UI.InputData.Description), setDirty: true);

        Assert.IsTrue(element.IsDirty,
            "RaisePropertyChange(name, setDirty: true) must promote a clean element to dirty.");
    }

    /// <summary>
    /// Same contract as <c>RaisePropertyChange</c>: <c>RecordPropertyChange(..., setDirty: false)</c>
    /// must not clear the dirty flag on an already-dirty element.
    /// </summary>
    [STATestMethod]
    public void RecordPropertyChange_SetDirtyFalse_PreservesExistingDirty()
    {
        var collection = new InputDataCollection(BestFitProject.GetInstance());
        var element = new UI.InputData("PreserveDirty-Record", collection);
        ForceSetIsDirty(element, true);
        Assert.IsTrue(element.IsDirty);

        // Use a real property name — PropertyChangeAction reflects the property
        // on the target type; an unknown name would throw before we can assert.
        // Disable undo recording so the action constructor is short-circuited
        // (reflection path). Then re-enable and verify dirty was preserved.
        element.IsUndoEnabled = false;
        try
        {
            InvokeRecordPropertyChange(element, nameof(UI.InputData.Description), "old", "new", setDirty: false);
        }
        finally { element.IsUndoEnabled = true; }

        Assert.IsTrue(element.IsDirty,
            "RecordPropertyChange(..., setDirty: false) must NOT clear IsDirty.");
    }

    /// <summary>
    /// Assigning a new <see cref="IProject.AvalonDockLayout"/> string must not clear
    /// <c>Project.IsDirty</c>. This is the concrete regression: MainWindow's
    /// <c>SaveLayout()</c> writes a fresh layout string through this setter as part
    /// of the Ctrl+S / close path. Before the fix, the setter's internal
    /// <c>RaisePropertyChange(name, false)</c> call force-cleared <c>IsDirty</c>,
    /// which caused <c>BestFitProject.Save()</c> to take the layout-only branch
    /// and skip saving dirty children.
    /// </summary>
    [STATestMethod]
    public void AvalonDockLayout_SetterDoesNotClearProjectIsDirty()
    {
        var project = BestFitProject.GetInstance();
        ForceSetIsDirty(project, true);
        Assert.IsTrue(project.IsDirty, "Precondition: project should start dirty.");

        string distinctLayoutXml = $"<LayoutRoot testMarker=\"{Guid.NewGuid():N}\" />";
        project.AvalonDockLayout = distinctLayoutXml;

        Assert.IsTrue(project.IsDirty,
            "Assigning AvalonDockLayout must not clear Project.IsDirty. The setter is " +
            "notify-only for dirty; LayoutDirty tracks the layout change separately.");
        Assert.IsTrue(project.LayoutDirty,
            "LayoutDirty must flip true so the Save gate treats the layout as needing persistence.");

        // Reset to leave project state clean for other tests.
        ForceSetIsDirty(project, false);
    }

    /// <summary>
    /// Same contract for the Project Explorer layout setter.
    /// </summary>
    [STATestMethod]
    public void ProjectExplorerLayout_SetterDoesNotClearProjectIsDirty()
    {
        var project = BestFitProject.GetInstance();
        ForceSetIsDirty(project, true);
        Assert.IsTrue(project.IsDirty);

        string distinctLayoutXml = $"<Node testMarker=\"{Guid.NewGuid():N}\" />";
        project.ProjectExplorerLayout = distinctLayoutXml;

        Assert.IsTrue(project.IsDirty,
            "Assigning ProjectExplorerLayout must not clear Project.IsDirty.");
        Assert.IsTrue(project.LayoutDirty);

        ForceSetIsDirty(project, false);
    }

    // --- Helpers ---------------------------------------------------------

    /// <summary>
    /// Invokes the protected <c>SetIsDirty(bool)</c> via reflection to set up a
    /// precondition without going through a full Save/Open lifecycle.
    /// </summary>
    private static void ForceSetIsDirty(object target, bool value)
    {
        var method = FindInstanceMethod(target.GetType(), "SetIsDirty", typeof(bool));
        method.Invoke(target, new object[] { value });
    }

    /// <summary>
    /// Invokes the protected <c>RaisePropertyChange(string, bool)</c> via reflection.
    /// Used by tests that verify the semantic of the <c>setDirty</c> parameter without
    /// relying on a specific property setter's side effects.
    /// </summary>
    private static void InvokeRaisePropertyChange(object target, string propertyName, bool setDirty)
    {
        var method = FindInstanceMethod(target.GetType(), "RaisePropertyChange", typeof(string), typeof(bool));
        method.Invoke(target, new object[] { propertyName, setDirty });
    }

    /// <summary>
    /// Invokes the protected <c>RecordPropertyChange(string, object, object, bool)</c>
    /// via reflection.
    /// </summary>
    private static void InvokeRecordPropertyChange(object target, string propertyName, object oldValue, object newValue, bool setDirty)
    {
        var method = FindInstanceMethod(
            target.GetType(),
            "RecordPropertyChange",
            typeof(string), typeof(object), typeof(object), typeof(bool));
        method.Invoke(target, new object[] { propertyName, oldValue, newValue, setDirty });
    }

    /// <summary>
    /// Walks the inheritance chain to find a non-public instance method by name and
    /// parameter types. Needed because reflection on a derived type does not see
    /// non-public members declared on base types by default.
    /// </summary>
    private static MethodInfo FindInstanceMethod(Type type, string name, params Type[] paramTypes)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            var m = t.GetMethod(name,
                BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                types: paramTypes,
                modifiers: null);
            if (m != null) return m;
        }
        throw new InvalidOperationException(
            $"Method '{name}({string.Join(", ", paramTypes.Select(p => p.Name))})' not found on {type.FullName} or any base.");
    }
}
