using FrameworkInterfaces.Undo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OxyPlot.Wpf;
using RMC.BestFit.UI;
using System;

namespace RMC.BestFit.UI.Tests.Elements.Support;

/// <summary>
/// Unit tests for <see cref="PlotUndoManager"/>, the per-plot owner of undo bridges
/// for plot, axis, series, and annotation visual properties.
/// </summary>
/// <remarks>
/// All tests require an STA thread because OxyPlot WPF <see cref="Plot"/> is a UserControl.
/// Tests cover constructor argument validation, the <see cref="PlotUndoManager.Plot"/> accessor,
/// <see cref="PlotUndoManager.SuspendRecording"/> nesting and idempotency,
/// <see cref="PlotUndoManager.RebuildSeriesAndAnnotationBridges"/> deferral while suspended,
/// and <see cref="IDisposable.Dispose"/> idempotency. Tests use <c>() =&gt; null</c> for the
/// undo-manager getter so no real undo manager is required.
/// </remarks>
[TestClass]
public class PlotUndoManagerTests
{
    /// <summary>
    /// Returns null for the undo manager — undo is effectively disabled, so bridges create
    /// shadow baselines but never push actions.
    /// </summary>
    private static IUndoManager? NullUndo() => null;

    /// <summary>
    /// No-op callback for "action recorded" — the constructor requires a non-null delegate.
    /// </summary>
    private static readonly Action NoOp = () => { };

    /// <summary>
    /// Verifies that the constructor throws <see cref="ArgumentNullException"/> when
    /// the <c>plot</c> argument is null.
    /// </summary>
    [STATestMethod]
    public void Constructor_NullPlot_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            new PlotUndoManager(null!, NullUndo, "test", new object(), NoOp));
    }

    /// <summary>
    /// Verifies that the constructor throws <see cref="ArgumentNullException"/> when
    /// the <c>getUndoManager</c> lambda is null.
    /// </summary>
    [STATestMethod]
    public void Constructor_NullGetUndoManager_ThrowsArgumentNullException()
    {
        var plot = new Plot();

        Assert.ThrowsException<ArgumentNullException>(() =>
            new PlotUndoManager(plot, null!, "test", new object(), NoOp));
    }

    /// <summary>
    /// Verifies that the constructor throws <see cref="ArgumentNullException"/> when
    /// the <c>onActionRecorded</c> callback is null.
    /// </summary>
    [STATestMethod]
    public void Constructor_NullOnActionRecorded_ThrowsArgumentNullException()
    {
        var plot = new Plot();

        Assert.ThrowsException<ArgumentNullException>(() =>
            new PlotUndoManager(plot, NullUndo, "test", new object(), null!));
    }

    /// <summary>
    /// Verifies that a null <c>description</c> is accepted (defaulted to "plot" internally)
    /// rather than throwing.
    /// </summary>
    [STATestMethod]
    public void Constructor_NullDescription_DoesNotThrow()
    {
        var plot = new Plot();

        // Description is optional/defaulted internally — must not throw on null.
        using var manager = new PlotUndoManager(plot, NullUndo, null!, new object(), NoOp);
    }

    /// <summary>
    /// Verifies that a null <c>target</c> is allowed (the target is an opaque payload
    /// for undo-action ownership).
    /// </summary>
    [STATestMethod]
    public void Constructor_NullTarget_DoesNotThrow()
    {
        var plot = new Plot();

        using var manager = new PlotUndoManager(plot, NullUndo, "test", null!, NoOp);
    }

    /// <summary>
    /// Verifies that the <see cref="PlotUndoManager.Plot"/> property exposes the
    /// instance passed to the constructor.
    /// </summary>
    [STATestMethod]
    public void Plot_Property_ReturnsConstructorPlot()
    {
        var plot = new Plot();
        using var manager = new PlotUndoManager(plot, NullUndo, "test", new object(), NoOp);

        Assert.AreSame(plot, manager.Plot);
    }

    /// <summary>
    /// Verifies that <see cref="PlotUndoManager.SuspendRecording"/> returns a non-null disposable.
    /// </summary>
    [STATestMethod]
    public void SuspendRecording_ReturnsNonNullDisposable()
    {
        var plot = new Plot();
        using var manager = new PlotUndoManager(plot, NullUndo, "test", new object(), NoOp);

        using var token = manager.SuspendRecording();

        Assert.IsNotNull(token);
    }

    /// <summary>
    /// Verifies that nested <see cref="PlotUndoManager.SuspendRecording"/> calls are safe —
    /// the inner scope is a no-op and only the outermost scope actually resumes recording.
    /// </summary>
    [STATestMethod]
    public void SuspendRecording_Nested_DoesNotThrow()
    {
        var plot = new Plot();
        using var manager = new PlotUndoManager(plot, NullUndo, "test", new object(), NoOp);

        using (var outer = manager.SuspendRecording())
        using (var inner = manager.SuspendRecording())
        {
            // Both scopes alive — no exception expected
        }

        // Both scopes have disposed; recording resumed normally.
    }

    /// <summary>
    /// Verifies that <see cref="PlotUndoManager.RebuildSeriesAndAnnotationBridges"/>
    /// is safe to call on a freshly constructed manager (no series / annotations to rebuild).
    /// </summary>
    [STATestMethod]
    public void RebuildSeriesAndAnnotationBridges_OnEmptyPlot_DoesNotThrow()
    {
        var plot = new Plot();
        using var manager = new PlotUndoManager(plot, NullUndo, "test", new object(), NoOp);

        manager.RebuildSeriesAndAnnotationBridges();
    }

    /// <summary>
    /// Verifies that calling <see cref="PlotUndoManager.RebuildSeriesAndAnnotationBridges"/>
    /// while suspended is safe — the rebuild is deferred until the outermost scope disposes.
    /// </summary>
    [STATestMethod]
    public void RebuildSeriesAndAnnotationBridges_WhileSuspended_DoesNotThrow()
    {
        var plot = new Plot();
        using var manager = new PlotUndoManager(plot, NullUndo, "test", new object(), NoOp);

        using (var token = manager.SuspendRecording())
        {
            // Calling rebuild inside the suspension scope must not throw —
            // the rebuild is deferred until the scope disposes.
            manager.RebuildSeriesAndAnnotationBridges();
        }
    }

    /// <summary>
    /// Verifies that <see cref="IDisposable.Dispose"/> is idempotent.
    /// </summary>
    [STATestMethod]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var plot = new Plot();
        var manager = new PlotUndoManager(plot, NullUndo, "test", new object(), NoOp);

        manager.Dispose();
        manager.Dispose();
    }

    /// <summary>
    /// Verifies that <see cref="PlotUndoManager.RebuildSeriesAndAnnotationBridges"/> throws
    /// <see cref="ObjectDisposedException"/> after the manager has been disposed.
    /// </summary>
    /// <remarks>
    /// UI-28 changed this from a silent no-op to an explicit throw. The silent path was hiding
    /// stale-reference bugs (App-side controls calling into a disposed manager when they should
    /// have been using the owning element's current bridge manager).
    /// </remarks>
    [STATestMethod]
    public void RebuildSeriesAndAnnotationBridges_AfterDispose_Throws()
    {
        var plot = new Plot();
        var manager = new PlotUndoManager(plot, NullUndo, "test", new object(), NoOp);
        manager.Dispose();

        Assert.ThrowsException<ObjectDisposedException>(() => manager.RebuildSeriesAndAnnotationBridges());
    }
}
