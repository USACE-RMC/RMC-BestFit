using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.Elements.Support;

/// <summary>
/// Unit tests for <see cref="DelegateDisposable"/>, an internal helper that invokes
/// a delegate exactly once on disposal.
/// </summary>
/// <remarks>
/// Accessible via <c>[assembly: InternalsVisibleTo("RMC.BestFit.UI.Tests")]</c> in the UI project.
/// </remarks>
[TestClass]
public class DelegateDisposableTests
{
    /// <summary>
    /// Verifies that the delegate is called exactly once when <see cref="DelegateDisposable.Dispose"/> is invoked.
    /// </summary>
    [TestMethod]
    public void Dispose_InvokesDelegate_ExactlyOnce()
    {
        int callCount = 0;
        var disposable = new DelegateDisposable(() => callCount++);

        disposable.Dispose();

        Assert.AreEqual(1, callCount);
    }

    /// <summary>
    /// Verifies that calling <see cref="DelegateDisposable.Dispose"/> a second time does not invoke
    /// the delegate again (idempotent behaviour).
    /// </summary>
    [TestMethod]
    public void Dispose_CalledTwice_DelegateInvokedOnlyOnce()
    {
        int callCount = 0;
        var disposable = new DelegateDisposable(() => callCount++);

        disposable.Dispose();
        disposable.Dispose();

        Assert.AreEqual(1, callCount);
    }

    /// <summary>
    /// Verifies that the delegate is invoked when used in a <c>using</c> block.
    /// </summary>
    [TestMethod]
    public void UsingBlock_InvokesDelegate_WhenBlockExits()
    {
        int callCount = 0;

        using (new DelegateDisposable(() => callCount++))
        {
            Assert.AreEqual(0, callCount, "Delegate must not fire before using block exits.");
        }

        Assert.AreEqual(1, callCount, "Delegate must fire exactly once when using block exits.");
    }

    /// <summary>
    /// Verifies that constructing with a null action throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_NullDelegate_ThrowsArgumentNullException()
    {
        _ = new DelegateDisposable(null!);
    }

    /// <summary>
    /// Verifies that the delegate closure correctly captures its enclosing variable.
    /// </summary>
    [TestMethod]
    public void Dispose_ClosureCaptureWorks()
    {
        var list = new List<int>();
        var disposable = new DelegateDisposable(() => list.Add(42));

        disposable.Dispose();

        CollectionAssert.AreEqual(new[] { 42 }, list);
    }
}
