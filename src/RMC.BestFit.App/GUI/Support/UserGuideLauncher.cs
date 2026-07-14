using Numerics.Data;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace RMC_BestFit
{
    /// <summary>
    /// Identifies the result of attempting to open the online RMC-BestFit user guide.
    /// </summary>
    /// <remarks>
    /// The result distinguishes an expected offline condition from a successful browser launch.
    /// Unexpected connectivity or process-launch failures are propagated to the caller.
    /// </remarks>
    internal enum UserGuideLaunchResult
    {
        /// <summary>
        /// The user guide was passed to the operating system for opening.
        /// </summary>
        Opened,

        /// <summary>
        /// The connectivity check did not find an available Internet connection.
        /// </summary>
        NoInternet
    }

    /// <summary>
    /// Checks Internet connectivity and opens the online RMC-BestFit user guide in the default browser.
    /// </summary>
    /// <remarks>
    /// Production calls reuse the bounded, asynchronous connectivity check provided by
    /// <see cref="TimeSeriesDownload"/>. The dependency-taking overload provides deterministic
    /// unit-test coverage without requiring live network access or starting an external process.
    /// </remarks>
    internal static class UserGuideLauncher
    {
        /// <summary>
        /// Canonical URL for the RMC-BestFit version 1.0 online user guide.
        /// </summary>
        internal const string UserGuideUrl = "https://usace-rmc.github.io/RMC-Software-Documentation/docs/desktop-applications/rmc-bestfit/users-guide/v1.0/welcome-to-rmc-bestfit/";

        /// <summary>
        /// Checks Internet connectivity and opens the online user guide in the default browser.
        /// </summary>
        /// <returns>
        /// A task whose result is <see cref="UserGuideLaunchResult.Opened"/> when the browser launch
        /// is requested, or <see cref="UserGuideLaunchResult.NoInternet"/> when no connection is available.
        /// </returns>
        /// <exception cref="System.ComponentModel.Win32Exception">
        /// Thrown when Windows cannot start the default browser.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the process cannot be started.
        /// </exception>
        /// <remarks>
        /// The URL is opened through the Windows shell so the user's configured default browser is honored.
        /// Unexpected failures are intentionally propagated for the WPF caller to log and report.
        /// </remarks>
        internal static Task<UserGuideLaunchResult> TryOpenAsync()
        {
            return TryOpenAsync(
                TimeSeriesDownload.IsConnectedToInternet,
                processStartInfo => Process.Start(processStartInfo));
        }

        /// <summary>
        /// Checks Internet connectivity and opens the online user guide using supplied dependencies.
        /// </summary>
        /// <param name="isInternetAvailable">Asynchronous function that reports whether Internet connectivity is available.</param>
        /// <param name="processLauncher">Action that starts the configured browser process.</param>
        /// <returns>
        /// A task whose result is <see cref="UserGuideLaunchResult.Opened"/> when the browser launch
        /// is requested, or <see cref="UserGuideLaunchResult.NoInternet"/> when no connection is available.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="isInternetAvailable"/> or <paramref name="processLauncher"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// This overload isolates external effects for unit testing. Exceptions from either supplied
        /// dependency are not suppressed so the UI layer can provide consistent logging and notification.
        /// </remarks>
        internal static async Task<UserGuideLaunchResult> TryOpenAsync(
            Func<Task<bool>> isInternetAvailable,
            Action<ProcessStartInfo> processLauncher)
        {
            if (isInternetAvailable == null) throw new ArgumentNullException(nameof(isInternetAvailable));
            if (processLauncher == null) throw new ArgumentNullException(nameof(processLauncher));

            if (!await isInternetAvailable())
            {
                return UserGuideLaunchResult.NoInternet;
            }

            processLauncher(new ProcessStartInfo
            {
                FileName = UserGuideUrl,
                UseShellExecute = true
            });

            return UserGuideLaunchResult.Opened;
        }
    }
}
