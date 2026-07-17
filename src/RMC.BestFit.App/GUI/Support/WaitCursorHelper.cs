using System;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

namespace RMC_BestFit
{
    /// <summary>
    /// Provides helpers for showing a visible wait cursor around synchronous WPF work.
    /// </summary>
    /// <remarks>
    /// Some lazy plot updates run synchronously on the UI thread. Updating the cursor before
    /// the work starts gives the operating system a chance to show the wait cursor even when
    /// the mouse is stationary.
    /// </remarks>
    internal static class WaitCursorHelper
    {
        /// <summary>
        /// Runs an action with the WPF wait cursor visible for the duration of the work.
        /// </summary>
        /// <param name="dispatcher">The dispatcher that owns the UI work.</param>
        /// <param name="action">The synchronous action to run while the wait cursor is shown.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="dispatcher"/> or <paramref name="action"/> is null.</exception>
        /// <remarks>
        /// The previous override cursor is restored at background priority so the render-priority
        /// wait-cursor frame is not immediately cleared by fast operations.
        /// </remarks>
        internal static void RunWithVisibleWaitCursor(Dispatcher dispatcher, Action action)
        {
            if (dispatcher == null) throw new ArgumentNullException(nameof(dispatcher));
            if (action == null) throw new ArgumentNullException(nameof(action));

            Cursor previousCursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = Cursors.Wait;
            Mouse.UpdateCursor();

            try
            {
                action();
            }
            finally
            {
                _ = dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    if (Mouse.OverrideCursor == Cursors.Wait)
                    {
                        Mouse.OverrideCursor = previousCursor;
                        Mouse.UpdateCursor();
                    }
                }));
            }
        }

        /// <summary>
        /// Runs asynchronous work with the WPF wait cursor visible before the work begins.
        /// </summary>
        /// <param name="dispatcher">The dispatcher that owns the UI work.</param>
        /// <param name="action">The asynchronous action to run while the wait cursor is shown.</param>
        /// <returns>A task that completes when the asynchronous action completes.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="dispatcher"/> or <paramref name="action"/> is null.</exception>
        /// <remarks>
        /// Yielding once at background priority lets WPF process higher-priority input and
        /// render work so the wait cursor and newly visible progress controls appear before
        /// analysis startup code can occupy the UI thread.
        /// </remarks>
        internal static async Task RunWithVisibleWaitCursorAsync(Dispatcher dispatcher, Func<Task> action)
        {
            if (dispatcher == null) throw new ArgumentNullException(nameof(dispatcher));
            if (action == null) throw new ArgumentNullException(nameof(action));

            Cursor previousCursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = Cursors.Wait;
            Mouse.UpdateCursor();

            try
            {
                await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                await action();
            }
            finally
            {
                _ = dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    if (Mouse.OverrideCursor == Cursors.Wait)
                    {
                        Mouse.OverrideCursor = previousCursor;
                        Mouse.UpdateCursor();
                    }
                }));
            }
        }
    }
}
