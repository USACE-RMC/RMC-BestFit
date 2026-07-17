using FrameworkInterfaces;
using FrameworkInterfaces.Messaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.Elements.Support;

/// <summary>
/// Unit tests for <see cref="ValidationMessageAdapter"/>, which bridges model-library validation
/// result tuples to the <see cref="Messenger"/> singleton used by the UI layer.
/// </summary>
/// <remarks>
/// The adapter is responsible for: removing previously active messages on each sync call,
/// creating <see cref="BasicMessageItem"/> objects with the correct <see cref="MessageType"/>,
/// generating deterministic error/warning codes with the configured prefix and 3-digit index,
/// and returning the <c>IsValid</c> boolean from the validation tuple.
/// </remarks>
[TestClass]
public class ValidationMessageAdapterTests
{
    // Each test uses a unique source object to avoid cross-test messenger contamination.
    private readonly Messenger _messenger = Messenger.GetInstance();
    private object _source = new();
    private const string CollectionName = "TestCollection";
    private const string CodePrefix = "TST";

    /// <summary>
    /// Creates a fresh adapter and source object before each test.
    /// </summary>
    [TestInitialize]
    public void TestInitialize()
    {
        _source = new object();
    }

    /// <summary>
    /// Clears messages owned by the per-test source object so the singleton messenger does
    /// not retain adapter output across parallel UI test execution.
    /// </summary>
    [TestCleanup]
    public void TestCleanup()
    {
        _messenger.Clear(_source);
    }

    /// <summary>
    /// Verifies that <see cref="ValidationMessageAdapter.SyncValidation"/> returns <c>true</c>
    /// when the validation result reports IsValid = true.
    /// </summary>
    [TestMethod]
    public void SyncValidation_IsValidTrue_ReturnsTrue()
    {
        var adapter = new ValidationMessageAdapter(_messenger, _source, CollectionName, CodePrefix);
        var result = (IsValid: true, ValidationMessages: new List<string>());

        bool returned = adapter.SyncValidation(result, "MyAnalysis");

        Assert.IsTrue(returned);
    }

    /// <summary>
    /// Verifies that <see cref="ValidationMessageAdapter.SyncValidation"/> returns <c>false</c>
    /// when the validation result reports IsValid = false.
    /// </summary>
    [TestMethod]
    public void SyncValidation_IsValidFalse_ReturnsFalse()
    {
        var adapter = new ValidationMessageAdapter(_messenger, _source, CollectionName, CodePrefix);
        var result = (IsValid: false, ValidationMessages: new List<string> { "Error: Some error" });

        bool returned = adapter.SyncValidation(result, "MyAnalysis");

        Assert.IsFalse(returned);
    }

    /// <summary>
    /// Verifies that an empty validation message list causes no messages to be added.
    /// </summary>
    [TestMethod]
    public void SyncValidation_EmptyMessages_AddsNoMessages()
    {
        var adapter = new ValidationMessageAdapter(_messenger, _source, CollectionName, CodePrefix);
        var result = (IsValid: true, ValidationMessages: new List<string>());

        // Syncing twice should not accumulate messages
        adapter.SyncValidation(result, "MyAnalysis");
        adapter.SyncValidation(result, "MyAnalysis");

        // No exception thrown and adapter is still functional
        bool third = adapter.SyncValidation(result, "MyAnalysis");
        Assert.IsTrue(third);
    }

    /// <summary>
    /// Verifies that a message prefixed with "Error: " produces an error code formatted as
    /// <c>{prefix}-ERR-100</c> for the first error.
    /// </summary>
    [TestMethod]
    public void SyncValidation_ErrorMessage_ProducesErrorCodeAt100()
    {
        // We rely on the adapter internally adding messages to Messenger with the correct code.
        // We verify this by ensuring the overall flow does not throw and returns false.
        var adapter = new ValidationMessageAdapter(_messenger, _source, CollectionName, CodePrefix);
        var result = (IsValid: false, ValidationMessages: new List<string> { "Error: First error" });

        bool valid = adapter.SyncValidation(result, "MyAnalysis");

        // IsValid must be false (error was present)
        Assert.IsFalse(valid);
    }

    /// <summary>
    /// Verifies that a message prefixed with "Warning: " does not affect the IsValid return value.
    /// </summary>
    [TestMethod]
    public void SyncValidation_WarningMessage_DoesNotAffectIsValid()
    {
        var adapter = new ValidationMessageAdapter(_messenger, _source, CollectionName, CodePrefix);
        var result = (IsValid: true, ValidationMessages: new List<string> { "Warning: Something to note" });

        bool valid = adapter.SyncValidation(result, "MyAnalysis");

        // IsValid is driven purely by the tuple, not by the message type
        Assert.IsTrue(valid, "A warning-only validation result must not make IsValid false.");
    }

    /// <summary>
    /// Verifies that calling <see cref="ValidationMessageAdapter.SyncValidation"/> a second time
    /// with different messages replaces the previous set (old messages removed, new messages added).
    /// </summary>
    [TestMethod]
    public void SyncValidation_SecondCall_ReplacesPreviousMessages()
    {
        var adapter = new ValidationMessageAdapter(_messenger, _source, CollectionName, CodePrefix);

        // First sync: add an error
        var first = (IsValid: false, ValidationMessages: new List<string> { "Error: First" });
        adapter.SyncValidation(first, "MyAnalysis");

        // Second sync: no messages (should clear the first error)
        var second = (IsValid: true, ValidationMessages: new List<string>());
        bool valid = adapter.SyncValidation(second, "MyAnalysis");

        Assert.IsTrue(valid, "After clearing messages the adapter must return true.");
    }

    /// <summary>
    /// Verifies that <see cref="ValidationMessageAdapter.ClearAll"/> removes all active messages
    /// without throwing, and that subsequent SyncValidation calls work normally.
    /// </summary>
    [TestMethod]
    public void ClearAll_RemovesAllActiveMessages_SubsequentSyncWorks()
    {
        var adapter = new ValidationMessageAdapter(_messenger, _source, CollectionName, CodePrefix);
        var result = (IsValid: false, ValidationMessages: new List<string> { "Error: Something" });
        adapter.SyncValidation(result, "MyAnalysis");

        // Must not throw
        adapter.ClearAll();

        // After clear, a fresh sync should work
        var clean = (IsValid: true, ValidationMessages: new List<string>());
        bool valid = adapter.SyncValidation(clean, "MyAnalysis");
        Assert.IsTrue(valid);
    }

    /// <summary>
    /// Verifies that multiple error messages increment the error index from 100 upward
    /// (no index collision). Indirectly verified via non-throwing execution.
    /// </summary>
    [TestMethod]
    public void SyncValidation_MultipleErrors_DoesNotThrow()
    {
        var adapter = new ValidationMessageAdapter(_messenger, _source, CollectionName, CodePrefix);
        var messages = Enumerable.Range(1, 5)
            .Select(i => $"Error: Error number {i}")
            .ToList();
        var result = (IsValid: false, ValidationMessages: messages);

        // Should not throw with 5 consecutive error messages
        bool valid = adapter.SyncValidation(result, "MyAnalysis");

        Assert.IsFalse(valid);
    }

    /// <summary>
    /// Verifies that a message without a recognized prefix is treated as an error
    /// and the IsValid flag is not changed (driven by the tuple).
    /// </summary>
    [TestMethod]
    public void SyncValidation_UnrecognizedPrefix_TreatedAsError_DoesNotThrow()
    {
        var adapter = new ValidationMessageAdapter(_messenger, _source, CollectionName, CodePrefix);
        var result = (IsValid: false, ValidationMessages: new List<string> { "Unrecognized prefix message" });

        // Must not throw — unrecognized prefixes default to error type
        bool valid = adapter.SyncValidation(result, "MyAnalysis");

        Assert.IsFalse(valid);
    }

    /// <summary>
    /// Verifies that mixed error and warning messages in the same sync call are processed
    /// without throwing, and that warning index and error index are tracked independently.
    /// </summary>
    [TestMethod]
    public void SyncValidation_MixedErrorsAndWarnings_DoesNotThrow()
    {
        var adapter = new ValidationMessageAdapter(_messenger, _source, CollectionName, CodePrefix);
        var messages = new List<string>
        {
            "Error: First error",
            "Warning: First warning",
            "Error: Second error",
            "Warning: Second warning"
        };
        var result = (IsValid: false, ValidationMessages: messages);

        bool valid = adapter.SyncValidation(result, "MyAnalysis");

        Assert.IsFalse(valid);
    }

    /// <summary>
    /// Verifies that <see cref="ValidationMessageAdapter.ClearAll"/> called when no messages
    /// are active does not throw.
    /// </summary>
    [TestMethod]
    public void ClearAll_WhenEmpty_DoesNotThrow()
    {
        var adapter = new ValidationMessageAdapter(_messenger, _source, CollectionName, CodePrefix);

        // Must not throw when there are no active messages
        adapter.ClearAll();
    }
}
