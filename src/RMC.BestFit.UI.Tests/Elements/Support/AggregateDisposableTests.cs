using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.Elements.Support;

/// <summary>
/// Unit tests for <see cref="AggregateDisposable"/>, which disposes a list of
/// <see cref="IDisposable"/> instances in reverse order.
/// </summary>
/// <remarks>
/// Accessible via <c>[assembly: InternalsVisibleTo("RMC.BestFit.UI.Tests")]</c> in the UI project.
/// </remarks>
[TestClass]
public class AggregateDisposableTests
{
    /// <summary>
    /// Verifies that all child disposables are disposed when <see cref="AggregateDisposable.Dispose"/> is called.
    /// </summary>
    [TestMethod]
    public void Dispose_DisposesAllChildren()
    {
        var order = new List<int>();
        var children = new List<IDisposable>
        {
            new DelegateDisposable(() => order.Add(1)),
            new DelegateDisposable(() => order.Add(2)),
            new DelegateDisposable(() => order.Add(3))
        };

        var aggregate = new AggregateDisposable(children);
        aggregate.Dispose();

        Assert.AreEqual(3, order.Count, "All 3 children must be disposed.");
    }

    /// <summary>
    /// Verifies that child disposables are disposed in reverse insertion order.
    /// </summary>
    /// <remarks>
    /// Reverse-order disposal mirrors <c>IDisposable</c> nesting semantics — the last acquired
    /// suspension token should be released first, matching LIFO resource management.
    /// </remarks>
    [TestMethod]
    public void Dispose_DisposesChildrenInReverseOrder()
    {
        var order = new List<int>();
        var children = new List<IDisposable>
        {
            new DelegateDisposable(() => order.Add(1)),
            new DelegateDisposable(() => order.Add(2)),
            new DelegateDisposable(() => order.Add(3))
        };

        var aggregate = new AggregateDisposable(children);
        aggregate.Dispose();

        CollectionAssert.AreEqual(new[] { 3, 2, 1 }, order,
            "Children must be disposed in reverse insertion order.");
    }

    /// <summary>
    /// Verifies that calling <see cref="AggregateDisposable.Dispose"/> multiple times does not
    /// dispose the children more than once (double-dispose safety).
    /// </summary>
    [TestMethod]
    public void Dispose_CalledTwice_ChildrenDisposedOnlyOnce()
    {
        int count = 0;
        var children = new List<IDisposable>
        {
            new DelegateDisposable(() => count++),
            new DelegateDisposable(() => count++)
        };

        var aggregate = new AggregateDisposable(children);
        aggregate.Dispose();
        aggregate.Dispose(); // should be no-op

        Assert.AreEqual(2, count, "Each child must be disposed exactly once despite two Dispose() calls.");
    }

    /// <summary>
    /// Verifies that an empty children list is handled without throwing.
    /// </summary>
    [TestMethod]
    public void Dispose_EmptyList_DoesNotThrow()
    {
        var aggregate = new AggregateDisposable(new List<IDisposable>());
        // Must not throw
        aggregate.Dispose();
    }

    /// <summary>
    /// Verifies that a null entry in the children list is skipped gracefully without a
    /// <see cref="NullReferenceException"/>.
    /// </summary>
    [TestMethod]
    public void Dispose_NullEntryInList_SkippedGracefully()
    {
        int count = 0;
        var children = new List<IDisposable>
        {
            new DelegateDisposable(() => count++),
            null!,
            new DelegateDisposable(() => count++)
        };

        var aggregate = new AggregateDisposable(children);
        // Should not throw NullReferenceException
        aggregate.Dispose();

        Assert.AreEqual(2, count, "Non-null children must still be disposed.");
    }

    /// <summary>
    /// Verifies that a single child is disposed correctly.
    /// </summary>
    [TestMethod]
    public void Dispose_SingleChild_IsDisposed()
    {
        bool disposed = false;
        var children = new List<IDisposable>
        {
            new DelegateDisposable(() => disposed = true)
        };

        var aggregate = new AggregateDisposable(children);
        aggregate.Dispose();

        Assert.IsTrue(disposed);
    }
}
