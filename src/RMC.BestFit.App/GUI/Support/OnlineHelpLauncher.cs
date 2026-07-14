using Numerics.Data;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace RMC_BestFit
{
    /// <summary>
    /// Identifies the result of attempting to open an online RMC-BestFit Help resource.
    /// </summary>
    /// <remarks>
    /// The result distinguishes an expected offline condition from a successful browser launch.
    /// Unexpected connectivity or process-launch failures are propagated to the caller.
    /// </remarks>
    internal enum OnlineHelpLaunchResult
    {
        /// <summary>
        /// The Help resource was passed to the operating system for opening.
        /// </summary>
        Opened,

        /// <summary>
        /// The connectivity check did not find an available Internet connection.
        /// </summary>
        NoInternet
    }

    /// <summary>
    /// Checks Internet connectivity and opens online RMC-BestFit Help resources in the default browser.
    /// </summary>
    /// <remarks>
    /// Production calls reuse the bounded, asynchronous connectivity check provided by
    /// <see cref="TimeSeriesDownload"/>. The dependency-taking overload provides deterministic
    /// unit-test coverage without requiring live network access or starting an external process.
    /// </remarks>
    internal static class OnlineHelpLauncher
    {
        /// <summary>
        /// Canonical URL for the RMC-BestFit version 1.0 online user guide.
        /// </summary>
        internal const string UserGuideUrl = "https://usace-rmc.github.io/RMC-Software-Documentation/docs/desktop-applications/rmc-bestfit/users-guide/v1.0/welcome-to-rmc-bestfit/";

        /// <summary>
        /// Canonical URL for the RMC-BestFit technical reference on the main branch.
        /// </summary>
        internal const string TechnicalReferenceUrl = "https://github.com/USACE-RMC/RMC-BestFit/blob/main/docs/index.md";

        /// <summary>
        /// Canonical URL for the RMC-BestFit example projects on the main branch.
        /// </summary>
        internal const string ExampleProjectsUrl = "https://github.com/USACE-RMC/RMC-BestFit/tree/main/examples";

        /// <summary>
        /// Checks Internet connectivity and opens an online Help resource in the default browser.
        /// </summary>
        /// <param name="url">Absolute HTTPS URL of the Help resource to open.</param>
        /// <returns>
        /// A task whose result is <see cref="OnlineHelpLaunchResult.Opened"/> when the browser launch
        /// is requested, or <see cref="OnlineHelpLaunchResult.NoInternet"/> when no connection is available.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="url"/> is empty or is not an absolute HTTPS URL.
        /// </exception>
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
        internal static Task<OnlineHelpLaunchResult> TryOpenAsync(string url)
        {
            return TryOpenAsync(
                url,
                TimeSeriesDownload.IsConnectedToInternet,
                processStartInfo => Process.Start(processStartInfo));
        }

        /// <summary>
        /// Checks Internet connectivity and opens an online Help resource using supplied dependencies.
        /// </summary>
        /// <param name="url">Absolute HTTPS URL of the Help resource to open.</param>
        /// <param name="isInternetAvailable">Asynchronous function that reports whether Internet connectivity is available.</param>
        /// <param name="processLauncher">Action that starts the configured browser process.</param>
        /// <returns>
        /// A task whose result is <see cref="OnlineHelpLaunchResult.Opened"/> when the browser launch
        /// is requested, or <see cref="OnlineHelpLaunchResult.NoInternet"/> when no connection is available.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="url"/> is empty or is not an absolute HTTPS URL.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="isInternetAvailable"/> or <paramref name="processLauncher"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// This overload isolates external effects for unit testing. Exceptions from either supplied
        /// dependency are not suppressed so the UI layer can provide consistent logging and notification.
        /// </remarks>
        internal static async Task<OnlineHelpLaunchResult> TryOpenAsync(
            string url,
            Func<Task<bool>> isInternetAvailable,
            Action<ProcessStartInfo> processLauncher)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("A Help resource URL is required.", nameof(url));
            }
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri helpUri) || helpUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new ArgumentException("The Help resource URL must be an absolute HTTPS URL.", nameof(url));
            }
            if (isInternetAvailable == null) throw new ArgumentNullException(nameof(isInternetAvailable));
            if (processLauncher == null) throw new ArgumentNullException(nameof(processLauncher));

            if (!await isInternetAvailable())
            {
                return OnlineHelpLaunchResult.NoInternet;
            }

            processLauncher(new ProcessStartInfo
            {
                FileName = helpUri.AbsoluteUri,
                UseShellExecute = true
            });

            return OnlineHelpLaunchResult.Opened;
        }
    }
}
