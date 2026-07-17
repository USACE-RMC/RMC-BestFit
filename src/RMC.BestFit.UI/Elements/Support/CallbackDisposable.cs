using System;

namespace RMC.BestFit.UI
{
    /// <summary>
    /// Wraps a callback <see cref="Action"/> as an <see cref="IDisposable"/>. The callback is
    /// invoked exactly once when <see cref="Dispose"/> is called; subsequent calls are no-ops.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// Functionally similar to <see cref="DelegateDisposable"/> — the difference is that
    /// CallbackDisposable accepts a null callback and silently no-ops on Dispose, whereas
    /// DelegateDisposable rejects null in its constructor with <see cref="ArgumentNullException"/>.
    /// CallbackDisposable is currently exercised only by unit tests; production code uses
    /// DelegateDisposable. Future refactor opportunity: collapse the two into one type.
    /// </para>
    /// </remarks>
    internal sealed class CallbackDisposable : IDisposable
    {
        /// <summary>
        /// The callback to invoke on dispose. Set to null after invocation to make
        /// subsequent <see cref="Dispose"/> calls no-ops.
        /// </summary>
        private Action _onDispose;

        /// <summary>
        /// Initializes a new instance of the <see cref="CallbackDisposable"/> class.
        /// </summary>
        /// <param name="onDispose">The callback to invoke when disposed. May be null.</param>
        public CallbackDisposable(Action onDispose) => _onDispose = onDispose;

        /// <summary>
        /// Invokes the callback (if non-null) and prevents subsequent calls from invoking it again.
        /// </summary>
        public void Dispose()
        {
            _onDispose?.Invoke();
            _onDispose = null;
        }
    }
}
