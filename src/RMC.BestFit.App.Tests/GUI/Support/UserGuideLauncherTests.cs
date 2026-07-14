using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RMC.BestFit.App.Tests.GUI.Support
{
    /// <summary>
    /// Tests the online user-guide launcher and its Help-menu integration.
    /// </summary>
    /// <remarks>
    /// External dependencies are supplied as delegates so the tests neither access the network
    /// nor open the user's default browser.
    /// </remarks>
    [TestClass]
    public class UserGuideLauncherTests
    {
        /// <summary>
        /// Verifies an available connection launches the canonical guide URL through the Windows shell.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <remarks>
        /// The captured process-start information verifies both URL stability and default-browser behavior.
        /// </remarks>
        [TestMethod]
        public async Task TryOpenAsync_WhenOnline_LaunchesCanonicalUrl()
        {
            ProcessStartInfo capturedStartInfo = null;
            int launchCount = 0;

            RMC_BestFit.UserGuideLaunchResult result = await RMC_BestFit.UserGuideLauncher.TryOpenAsync(
                () => Task.FromResult(true),
                startInfo =>
                {
                    launchCount++;
                    capturedStartInfo = startInfo;
                });

            Assert.AreEqual(RMC_BestFit.UserGuideLaunchResult.Opened, result);
            Assert.AreEqual(1, launchCount);
            Assert.IsNotNull(capturedStartInfo);
            Assert.AreEqual(RMC_BestFit.UserGuideLauncher.UserGuideUrl, capturedStartInfo.FileName);
            Assert.IsTrue(capturedStartInfo.UseShellExecute);
        }

        /// <summary>
        /// Verifies an unavailable connection returns the offline result without launching a process.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <remarks>
        /// Avoiding the process call ensures the UI can show a focused connection message instead.
        /// </remarks>
        [TestMethod]
        public async Task TryOpenAsync_WhenOffline_DoesNotLaunchProcess()
        {
            bool processWasLaunched = false;

            RMC_BestFit.UserGuideLaunchResult result = await RMC_BestFit.UserGuideLauncher.TryOpenAsync(
                () => Task.FromResult(false),
                startInfo => processWasLaunched = true);

            Assert.AreEqual(RMC_BestFit.UserGuideLaunchResult.NoInternet, result);
            Assert.IsFalse(processWasLaunched);
        }

        /// <summary>
        /// Verifies both required external-dependency delegates are validated.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <remarks>
        /// Explicit validation prevents deferred null-reference failures inside an asynchronous menu handler.
        /// </remarks>
        [TestMethod]
        public async Task TryOpenAsync_WithNullDependencies_ThrowsArgumentNullException()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() =>
                RMC_BestFit.UserGuideLauncher.TryOpenAsync(
                    null,
                    startInfo => { }));

            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() =>
                RMC_BestFit.UserGuideLauncher.TryOpenAsync(
                    () => Task.FromResult(true),
                    null));
        }

        /// <summary>
        /// Verifies unexpected connectivity-check failures propagate to the WPF caller.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <remarks>
        /// The menu handler owns user notification and diagnostic logging for unexpected failures.
        /// </remarks>
        [TestMethod]
        public async Task TryOpenAsync_WhenConnectivityCheckFails_PropagatesException()
        {
            var expected = new InvalidOperationException("Connectivity failure.");

            InvalidOperationException actual = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                RMC_BestFit.UserGuideLauncher.TryOpenAsync(
                    () => Task.FromException<bool>(expected),
                    startInfo => Assert.Fail("The process launcher must not run after a connectivity failure.")));

            Assert.AreSame(expected, actual);
        }

        /// <summary>
        /// Verifies unexpected process-launch failures propagate to the WPF caller.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <remarks>
        /// The menu handler catches this failure and restores the menu item before displaying an error.
        /// </remarks>
        [TestMethod]
        public async Task TryOpenAsync_WhenProcessLaunchFails_PropagatesException()
        {
            var expected = new InvalidOperationException("Process failure.");

            InvalidOperationException actual = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                RMC_BestFit.UserGuideLauncher.TryOpenAsync(
                    () => Task.FromResult(true),
                    startInfo => throw expected));

            Assert.AreSame(expected, actual);
        }

        /// <summary>
        /// Verifies the Help menu uses the online launcher and no longer references the legacy PDF.
        /// </summary>
        /// <remarks>
        /// This source-level integration guard avoids constructing the framework-owned main WPF window.
        /// </remarks>
        [TestMethod]
        public void DefineHelpMenuItems_UsesOnlineUserGuideLauncher()
        {
            string source = ReadMainProjectNodeSource();

            StringAssert.Contains(source, "Header = \"User Guide\"");
            StringAssert.Contains(source, "await UserGuideLauncher.TryOpenAsync()");
            StringAssert.Contains(source, "userGuideItem.IsEnabled = false;");
            StringAssert.Contains(source, "userGuideItem.IsEnabled = true;");
            StringAssert.Contains(source, "No internet connection is available. Please check your connection and try again.");
            StringAssert.Contains(source, "Something went wrong. Cannot open the RMC-BestFit User Guide.");
            Assert.IsFalse(source.Contains("Quick Start Guide", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains(".pdf", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Reads the <c>MainProjectNode</c> source file from the repository.
        /// </summary>
        /// <param name="sourceFilePath">The compiler-provided path to this test source file.</param>
        /// <returns>The source text for <c>MainProjectNode.cs</c>.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the compiler-provided source directory cannot be determined.
        /// </exception>
        /// <remarks>
        /// The path is resolved relative to the checked-in test source so the test does not depend on
        /// the process working directory or build-output layout.
        /// </remarks>
        private static string ReadMainProjectNodeSource([CallerFilePath] string sourceFilePath = "")
        {
            string sourceDirectory = Path.GetDirectoryName(sourceFilePath);
            if (string.IsNullOrEmpty(sourceDirectory))
            {
                throw new InvalidOperationException("Could not determine the test source directory.");
            }

            string mainProjectNodePath = Path.GetFullPath(Path.Combine(
                sourceDirectory,
                "..",
                "..",
                "..",
                "RMC.BestFit.App",
                "GUI",
                "MainProjectNode.cs"));

            return File.ReadAllText(mainProjectNodePath);
        }
    }
}
