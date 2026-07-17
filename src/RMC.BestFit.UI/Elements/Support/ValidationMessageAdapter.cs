using FrameworkInterfaces;
using FrameworkInterfaces.Messaging;
using System.Collections.Generic;

namespace RMC.BestFit.UI
{
    /// <summary>
    /// Bridges model library validation messages to the UI messaging system.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// Model library analyses return validation results as <c>(bool IsValid, List&lt;string&gt; ValidationMessages)</c>
    /// tuples, where each string is prefixed with "Error: " or "Warning: " to indicate severity.
    /// The UI layer uses <see cref="BasicMessageItem"/> objects managed through a <see cref="Messenger"/> singleton.
    /// This adapter translates between the two representations, keeping the messenger in sync
    /// with the current set of model validation messages.
    /// </para>
    /// </remarks>
    public class ValidationMessageAdapter
    {
        /// <summary>
        /// The messenger singleton used to display messages in the UI.
        /// </summary>
        private readonly Messenger _messenger;

        /// <summary>
        /// The source object that owns these validation messages (typically the UI analysis element).
        /// </summary>
        private readonly object _source;

        /// <summary>
        /// The name of the parent collection in the project library (e.g., "UnivariateAnalyses").
        /// </summary>
        private readonly string _sourceCollectionName;

        /// <summary>
        /// The error code prefix used for generated message codes (e.g., "UDA", "RCA").
        /// </summary>
        private readonly string _codePrefix;

        /// <summary>
        /// The current set of <see cref="BasicMessageItem"/> instances managed by this adapter.
        /// These are kept in sync with the model's validation output.
        /// </summary>
        private readonly List<BasicMessageItem> _activeMessages = new List<BasicMessageItem>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationMessageAdapter"/> class.
        /// </summary>
        /// <param name="messenger">The messenger singleton used to display messages in the UI.</param>
        /// <param name="source">The source object that owns these validation messages (typically the UI analysis element).</param>
        /// <param name="sourceCollectionName">The name of the parent collection in the project library.</param>
        /// <param name="codePrefix">The error code prefix used for generated message codes (e.g., "UDA", "RCA").</param>
        public ValidationMessageAdapter(Messenger messenger, object source, string sourceCollectionName, string codePrefix)
        {
            _messenger = messenger;
            _source = source;
            _sourceCollectionName = sourceCollectionName;
            _codePrefix = codePrefix;
        }

        /// <summary>
        /// Synchronizes the UI messenger with the current model validation results.
        /// </summary>
        /// <param name="validationResult">The validation result tuple from the model library's <c>Validate()</c> method.</param>
        /// <param name="sourceName">The current name of the source element (may change when the user renames the analysis).</param>
        /// <returns><c>true</c> if the model validation passed; otherwise <c>false</c>.</returns>
        /// <remarks>
        /// <para>
        /// This method performs the following steps:
        /// </para>
        /// <list type="number">
        /// <item><description>Removes all previously active messages from the messenger.</description></item>
        /// <item><description>Clears the internal tracking list.</description></item>
        /// <item><description>Creates a new <see cref="BasicMessageItem"/> for each validation message string.</description></item>
        /// <item><description>Adds the new messages to the messenger.</description></item>
        /// </list>
        /// <para>
        /// Message strings from the model are expected to follow the convention:
        /// <c>"Error: &lt;message text&gt;"</c> or <c>"Warning: &lt;message text&gt;"</c>.
        /// The prefix determines the <see cref="MessageType"/>. Strings without a recognized
        /// prefix are treated as errors.
        /// </para>
        /// </remarks>
        public bool SyncValidation((bool IsValid, List<string> ValidationMessages) validationResult, string sourceName)
        {
            // Remove all previously active messages from the messenger
            for (int i = 0; i < _activeMessages.Count; i++)
            {
                _messenger.Remove(_activeMessages[i]);
            }
            _activeMessages.Clear();

            // Create new messages from the model validation output
            // Start at 100 to avoid collisions with UI-defined message codes (e.g., DFA-ERR-005..009)
            int errorIndex = 100;
            int warningIndex = 100;
            for (int i = 0; i < validationResult.ValidationMessages.Count; i++)
            {
                string raw = validationResult.ValidationMessages[i];
                MessageType messageType;
                string messageText;
                string code;

                if (raw.StartsWith("Warning: "))
                {
                    messageType = MessageType.Warning;
                    messageText = raw.Substring("Warning: ".Length);
                    code = _codePrefix + "-WRN-" + warningIndex.ToString("D3");
                    warningIndex++;
                }
                else if (raw.StartsWith("Error: "))
                {
                    messageType = MessageType.Error;
                    messageText = raw.Substring("Error: ".Length);
                    code = _codePrefix + "-ERR-" + errorIndex.ToString("D3");
                    errorIndex++;
                }
                else
                {
                    // Default to error for unrecognized prefixes
                    messageType = MessageType.Error;
                    messageText = raw;
                    code = _codePrefix + "-ERR-" + errorIndex.ToString("D3");
                    errorIndex++;
                }

                var item = new BasicMessageItem(messageType, messageText, _source, _sourceCollectionName, sourceName, "", code);
                _activeMessages.Add(item);
                _messenger.Add(item);
            }

            return validationResult.IsValid;
        }

        /// <summary>
        /// Removes all active messages from the messenger and clears the internal tracking list.
        /// </summary>
        /// <remarks>
        /// Call this method when the analysis element is being deleted or disposed to ensure
        /// no stale messages remain in the global messenger.
        /// </remarks>
        public void ClearAll()
        {
            for (int i = 0; i < _activeMessages.Count; i++)
            {
                _messenger.Remove(_activeMessages[i]);
            }
            _activeMessages.Clear();
        }
    }
}
