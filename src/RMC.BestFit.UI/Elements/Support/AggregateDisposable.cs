using System;
using System.Collections.Generic;

namespace RMC.BestFit.UI
{
    /// <summary>
    /// Aggregates multiple <see cref="IDisposable"/> instances into a single disposable that
    /// disposes them all in reverse order when disposed. Used by <c>SuspendPlotBridges()</c>
    /// methods across UI element classes to collect and manage suspension tokens from
    /// multiple <see cref="PlotUndoManager"/> instances.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// This class is safe against double-dispose: once disposed, subsequent calls to
    /// <see cref="Dispose"/> are no-ops. Disposes are performed in reverse insertion order
    /// to mirror try/finally unwinding semantics.
    /// </para>
    /// </remarks>
    internal sealed class AggregateDisposable : IDisposable
    {
        /// <summary>
        /// The list of disposable objects to aggregate.
        /// </summary>
        private readonly List<IDisposable> _disposables;

        /// <summary>
        /// Indicates whether this instance has already been disposed.
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="AggregateDisposable"/> class.
        /// </summary>
        /// <param name="disposables">The list of disposable objects to aggregate.</param>
        public AggregateDisposable(List<IDisposable> disposables) => _disposables = disposables;

        /// <summary>
        /// Disposes all aggregated objects in reverse order. Safe to call multiple times.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (int i = _disposables.Count - 1; i >= 0; i--)
                _disposables[i]?.Dispose();
        }
    }
}
