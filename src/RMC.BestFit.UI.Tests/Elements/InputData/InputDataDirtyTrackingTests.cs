using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.Models;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.Elements.InputData;

/// <summary>
/// Tests the invariants that keep <see cref="FrameworkInterfaces.ISave.IsDirty"/> (and therefore
/// <see cref="FrameworkInterfaces.IMetaData.LastModified"/>) accurate on <see cref="UI.InputData"/>.
/// </summary>
/// <remarks>
/// Background: <c>LastModified</c> should change only when the user actually edits
/// an element and that edit is persisted to disk. To keep the on-disk timestamp
/// honest, the in-memory <c>IsDirty</c> flag must not flip to <c>true</c> for
/// cascades that happen during <c>Open()</c>, <c>Copy()</c>, UI binding, or undo/redo
/// replay. These tests pin the observable contract.
/// </remarks>
[TestClass]
public class InputDataDirtyTrackingTests
{
    private static InputDataCollection? _collection;

    /// <summary>
    /// Creates a shared collection backed by the singleton BestFitProject.
    /// </summary>
    [ClassInitialize]
    public static void ClassInitialize(TestContext _)
    {
        _collection = new InputDataCollection(BestFitProject.GetInstance());
    }

    /// <summary>
    /// A freshly constructed InputData (v2+ path) must not be dirty — the constructor
    /// is not a user edit, and clean state is required for "just opening a project"
    /// not to stamp <c>LastModified</c> on close.
    /// </summary>
    [STATestMethod]
    public void Constructor_NotOpenedFromV1_IsNotDirty()
    {
        var id = new UI.InputData("CleanCtor", _collection!);

        Assert.IsFalse(id.IsDirty, "A freshly constructed v2+ InputData must start clean.");
    }

    /// <summary>
    /// With <c>IsUndoEnabled = false</c>, a property setter that goes through
    /// <c>RecordPropertyChange</c> (UnitLabel) must not flip <c>IsDirty</c>.
    /// This is the gate that prevents post-Open plot-axis-title cascades, undo
    /// replay, and Copy() / UI-binding paths from polluting the dirty flag.
    /// </summary>
    [STATestMethod]
    public void RecordPropertyChange_WithIsUndoEnabledFalse_DoesNotSetIsDirty()
    {
        var id = new UI.InputData("NoDirtyOnLoad", _collection!);
        Assert.IsFalse(id.IsDirty);

        id.IsUndoEnabled = false;
        try
        {
            id.UnitLabel = "Flow (CFS)";
        }
        finally
        {
            id.IsUndoEnabled = true;
        }

        Assert.IsFalse(
            id.IsDirty,
            "IsDirty must stay false when a RecordPropertyChange-based setter fires " +
            "with IsUndoEnabled=false (load / copy / UI-bind context).");
    }

    /// <summary>
    /// With <c>IsUndoEnabled = true</c>, the same <c>RecordPropertyChange</c>-based
    /// setter must flip <c>IsDirty</c> — this is the real user-edit path.
    /// </summary>
    [STATestMethod]
    public void RecordPropertyChange_WithIsUndoEnabledTrue_SetsIsDirty()
    {
        var id = new UI.InputData("DirtyOnEdit", _collection!);
        Assert.IsFalse(id.IsDirty);
        Assert.IsTrue(id.IsUndoEnabled);

        id.UnitLabel = "Stage (FT)";

        Assert.IsTrue(
            id.IsDirty,
            "A user edit (IsUndoEnabled=true) through a RecordPropertyChange-based setter must mark the element dirty.");
    }

    /// <summary>
    /// Simulates the bulk-paste cascade on the exact-data series: suppress
    /// CollectionChanged, mutate the series, then raise a single Reset. The cascade
    /// chain <c>Reset → DataFrame.RaisePropertyChange(ExactSeries) → InputData.DataFramePropertyChanged
    /// → Element.RaisePropertyChange</c> must flip <c>IsDirty</c> to <c>true</c>.
    /// Guards against narrowed Change 1 inadvertently breaking paste.
    /// </summary>
    [STATestMethod]
    public void BulkPasteResetCascade_MarksElementDirty()
    {
        var id = new UI.InputData("PasteCascade", _collection!);
        Assert.IsFalse(id.IsDirty);
        Assert.IsTrue(id.IsUndoEnabled);

        // Simulate what InputDataControl does during a DataGrid paste:
        //   PreviewPasteData:     SuppressCollectionChanged = true
        //   <rows added by DataGrid paste>
        //   DataPasted:           SuppressCollectionChanged = false
        //                         RaiseCollectionChangedReset()
        id.DataFrame.ExactSeries.SuppressCollectionChanged = true;
        for (int i = 0; i < 3; i++)
        {
            id.DataFrame.ExactSeries.Add(new ExactData(i + 1, 10.0 + i));
        }
        id.DataFrame.ExactSeries.SuppressCollectionChanged = false;
        id.DataFrame.ExactSeries.RaiseCollectionChangedReset();

        Assert.IsTrue(
            id.IsDirty,
            "Bulk paste (Reset → DataFrame.PropertyChanged → Element.RaisePropertyChange) must mark the element dirty " +
            "so that the subsequent Save() stamps LastModified.");
    }

    /// <summary>
    /// <c>ProjectBase.RaisePropertyChange</c> is the legacy PropertyChanged plumbing
    /// and must continue to mark the element dirty regardless of <c>IsUndoEnabled</c>.
    /// The narrowed fix targets only <c>RecordPropertyChange</c>; this test pins the
    /// legacy behavior so future refactors don't quietly change it.
    /// </summary>
    /// <remarks>
    /// Exercises the <c>DataFramePropertyChanged</c> forwarder, which is the canonical
    /// <c>RaisePropertyChange</c>-with-default-isDirty=true path. With IsUndoEnabled=false,
    /// if RaisePropertyChange were accidentally gated, the cascade would fail to dirty.
    /// </remarks>
    [STATestMethod]
    public void RaisePropertyChange_WithIsUndoEnabledFalse_StillSetsIsDirty()
    {
        var id = new UI.InputData("LegacyRaise", _collection!);
        Assert.IsFalse(id.IsDirty);

        id.IsUndoEnabled = false;
        try
        {
            // DataFrame.RaisePropertyChange → InputData.DataFramePropertyChanged
            //   → InputData.RaisePropertyChange(e.PropertyName, default isDirty=true)
            // This is the legacy path; must NOT be affected by IsUndoEnabled.
            id.DataFrame.ExactSeries.Add(new ExactData(1, 5.0));
        }
        finally
        {
            id.IsUndoEnabled = true;
        }

        Assert.IsTrue(
            id.IsDirty,
            "RaisePropertyChange must continue to flip IsDirty even when IsUndoEnabled=false. " +
            "Only RecordPropertyChange is gated; changing RaisePropertyChange would break " +
            "legacy PropertyChanged plumbing that predates the undo system.");
    }
}
