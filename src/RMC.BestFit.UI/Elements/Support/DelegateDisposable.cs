using System;

namespace RMC.BestFit.UI
{
    /// <summary>
    /// An <see cref="IDisposable"/> that invokes a delegate when disposed.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// Used for suspension patterns where a cleanup action must run when a using block exits.
    /// The delegate is invoked exactly once; subsequent Dispose calls are no-ops.
    /// </para>
    /// </remarks>
    internal sealed class DelegateDisposable : IDisposable
    {
        /// <summary>The action to invoke on the first <see cref="Dispose"/> call. Set to null after invocation to make subsequent calls a no-op.</summary>
        private Action _onDispose;

        /// <summary>
        /// Initializes a new instance with the specified dispose action.
        /// </summary>
        /// <param name="onDispose">The action to invoke when <see cref="Dispose"/> is called.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="onDispose"/> is null.</exception>
        public DelegateDisposable(Action onDispose)
        {
            _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
        }

        /// <summary>
        /// Invokes the dispose action. Safe to call multiple times.
        /// </summary>
        public void Dispose()
        {
            var action = _onDispose;
            _onDispose = null;
            action?.Invoke();
        }
    }
}
