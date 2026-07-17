using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.Elements.Support;

/// <summary>
/// Unit tests for <see cref="CallbackDisposable"/>, an internal helper that invokes
/// a callback action exactly once on disposal.
/// </summary>
/// <remarks>
/// <see cref="CallbackDisposable"/> is similar to <see cref="DelegateDisposable"/> but
/// sets the delegate to null after the first invocation rather than using a local variable swap.
/// Accessible via <c>[assembly: InternalsVisibleTo("RMC.BestFit.UI.Tests")]</c>.
/// </remarks>
[TestClass]
public class CallbackDisposableTests
{
    /// <summary>
    /// Verifies that the callback is invoked exactly once when <see cref="CallbackDisposable.Dispose"/>
    /// is called for the first time.
    /// </summary>
    [TestMethod]
    public void Dispose_InvokesCallback_ExactlyOnce()
    {
        int count = 0;
        var cd = new CallbackDisposable(() => count++);

        cd.Dispose();

        Assert.AreEqual(1, count);
    }

    /// <summary>
    /// Verifies that calling <see cref="CallbackDisposable.Dispose"/> a second time does not invoke
    /// the callback again — the callback field is nulled out after the first call.
    /// </summary>
    [TestMethod]
    public void Dispose_CalledTwice_CallbackInvokedOnlyOnce()
    {
        int count = 0;
        var cd = new CallbackDisposable(() => count++);

        cd.Dispose();
        cd.Dispose();

        Assert.AreEqual(1, count, "Callback must fire only once even when Dispose is called twice.");
    }

    /// <summary>
    /// Verifies that the callback executes immediately when the <c>using</c> block exits.
    /// </summary>
    [TestMethod]
    public void UsingBlock_InvokesCallback_OnExit()
    {
        int count = 0;

        using (new CallbackDisposable(() => count++))
        {
            Assert.AreEqual(0, count);
        }

        Assert.AreEqual(1, count);
    }

    /// <summary>
    /// Verifies that a null callback argument does not cause a NullReferenceException when
    /// <see cref="CallbackDisposable.Dispose"/> is called (the implementation guards with null-check).
    /// </summary>
    [TestMethod]
    public void Dispose_NullCallback_DoesNotThrow()
    {
        // CallbackDisposable does not throw on null _onDispose — it uses ?. invoke
        var cd = new CallbackDisposable(null!);
        cd.Dispose(); // should not throw
    }

    /// <summary>
    /// Verifies that a callback closure can capture and mutate external state correctly.
    /// </summary>
    [TestMethod]
    public void Dispose_ClosureCapture_WorksCorrectly()
    {
        var messages = new List<string>();
        var cd = new CallbackDisposable(() => messages.Add("disposed"));

        cd.Dispose();

        CollectionAssert.AreEqual(new[] { "disposed" }, messages);
    }
}
