using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RMC.BestFit.App.Tests.GUI.Support
{
    /// <summary>
    /// Tests the online Help launcher and its Help-menu and documentation integration.
    /// </summary>
    /// <remarks>
    /// External dependencies are supplied as delegates so the tests neither access the network
    /// nor open the user's default browser.
    /// </remarks>
    [TestClass]
    public class OnlineHelpLauncherTests
    {
        /// <summary>
        /// Verifies an available connection launches the requested HTTPS URL through the Windows shell.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <remarks>
        /// The captured process-start information verifies URL forwarding and default-browser behavior.
        /// </remarks>
        [TestMethod]
        public async Task TryOpenAsync_WhenOnline_LaunchesRequestedUrl()
        {
            const string requestedUrl = "https://example.test/help";
            ProcessStartInfo capturedStartInfo = null;
            int launchCount = 0;

            RMC_BestFit.OnlineHelpLaunchResult result = await RMC_BestFit.OnlineHelpLauncher.TryOpenAsync(
                requestedUrl,
                () => Task.FromResult(true),
                startInfo =>
                {
                    launchCount++;
                    capturedStartInfo = startInfo;
                });

            Assert.AreEqual(RMC_BestFit.OnlineHelpLaunchResult.Opened, result);
            Assert.AreEqual(1, launchCount);
            Assert.IsNotNull(capturedStartInfo);
            Assert.AreEqual(requestedUrl, capturedStartInfo.FileName);
            Assert.IsTrue(capturedStartInfo.UseShellExecute);
        }

        /// <summary>
        /// Verifies the canonical Help URLs target the intended user guide and main-branch resources.
        /// </summary>
        /// <remarks>
        /// Branch-based URLs keep the technical reference and examples current after changes merge to main.
        /// </remarks>
        [TestMethod]
        public void CanonicalUrls_TargetPublishedResources()
        {
            Assert.AreEqual(
                "https://usace-rmc.github.io/RMC-Software-Documentation/docs/desktop-applications/rmc-bestfit/users-guide/v1.0/welcome-to-rmc-bestfit/",
                RMC_BestFit.OnlineHelpLauncher.UserGuideUrl);
            Assert.AreEqual(
                "https://github.com/USACE-RMC/RMC-BestFit/blob/main/docs/index.md",
                RMC_BestFit.OnlineHelpLauncher.TechnicalReferenceUrl);
            Assert.AreEqual(
                "https://github.com/USACE-RMC/RMC-BestFit/tree/main/examples",
                RMC_BestFit.OnlineHelpLauncher.ExampleProjectsUrl);
            Assert.AreEqual(
                "https://doi.org/10.5281/zenodo.21301036",
                RMC_BestFit.OnlineHelpLauncher.ZenodoConceptDoiUrl);
            Assert.IsFalse(RMC_BestFit.OnlineHelpLauncher.TechnicalReferenceUrl.Contains("version-2-code-migration", StringComparison.Ordinal));
            Assert.IsFalse(RMC_BestFit.OnlineHelpLauncher.ExampleProjectsUrl.Contains("version-2-code-migration", StringComparison.Ordinal));
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

            RMC_BestFit.OnlineHelpLaunchResult result = await RMC_BestFit.OnlineHelpLauncher.TryOpenAsync(
                "https://example.test/help",
                () => Task.FromResult(false),
                startInfo => processWasLaunched = true);

            Assert.AreEqual(RMC_BestFit.OnlineHelpLaunchResult.NoInternet, result);
            Assert.IsFalse(processWasLaunched);
        }

        /// <summary>
        /// Verifies empty, relative, and non-HTTPS URLs are rejected before external work begins.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <remarks>
        /// Restricting Help targets to absolute HTTPS URLs prevents accidental shell launches of unsafe schemes.
        /// </remarks>
        [TestMethod]
        public async Task TryOpenAsync_WithInvalidUrl_ThrowsArgumentException()
        {
            string[] invalidUrls = { null, "", " ", "docs/index.md", "http://example.test/help" };

            foreach (string invalidUrl in invalidUrls)
            {
                await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                    RMC_BestFit.OnlineHelpLauncher.TryOpenAsync(
                        invalidUrl,
                        () => Task.FromResult(true),
                        startInfo => Assert.Fail("Invalid URLs must not be launched.")));
            }
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
                RMC_BestFit.OnlineHelpLauncher.TryOpenAsync(
                    "https://example.test/help",
                    null,
                    startInfo => { }));

            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() =>
                RMC_BestFit.OnlineHelpLauncher.TryOpenAsync(
                    "https://example.test/help",
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
                RMC_BestFit.OnlineHelpLauncher.TryOpenAsync(
                    "https://example.test/help",
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
                RMC_BestFit.OnlineHelpLauncher.TryOpenAsync(
                    "https://example.test/help",
                    () => Task.FromResult(true),
                    startInfo => throw expected));

            Assert.AreSame(expected, actual);
        }

        /// <summary>
        /// Verifies the Help menu contains the three online resources in the intended order.
        /// </summary>
        /// <remarks>
        /// This source-level integration guard avoids constructing the framework-owned main WPF window.
        /// </remarks>
        [TestMethod]
        public void DefineHelpMenuItems_UsesOnlineHelpLauncher()
        {
            string source = ReadRepositoryFile("src/RMC.BestFit.App/GUI/MainProjectNode.cs");
            const string userGuideCall = "CreateOnlineHelpMenuItem(\"User Guide\", OnlineHelpLauncher.UserGuideUrl, showHelpIcon: true)";
            const string technicalReferenceCall = "CreateOnlineHelpMenuItem(\"Technical Reference\", OnlineHelpLauncher.TechnicalReferenceUrl)";
            const string exampleProjectsCall = "CreateOnlineHelpMenuItem(\"Example Projects\", OnlineHelpLauncher.ExampleProjectsUrl)";
            const string separatorCall = "_helpMenuItems.Add(CreateHelpMenuSeparator());";

            StringAssert.Contains(source, userGuideCall);
            StringAssert.Contains(source, technicalReferenceCall);
            StringAssert.Contains(source, exampleProjectsCall);
            StringAssert.Contains(source, separatorCall);
            StringAssert.Contains(source, "private MenuItem CreateOnlineHelpMenuItem");
            StringAssert.Contains(source, "TermsDocument = TermsAndConditionsDocumentFactory.Create(CitationLink_RequestNavigate)");
            StringAssert.Contains(source, "ShowButtons = false");
            StringAssert.Contains(source, "EnableTermsDocumentLinks(window)");
            StringAssert.Contains(source, "window.FindName(\"TCURichTextBox\") is RichTextBox termsTextBox");
            StringAssert.Contains(source, "termsTextBox.IsDocumentEnabled = true");
            StringAssert.Contains(source, "await OpenOnlineResourceAsync(header, url)");
            StringAssert.Contains(source, "await OnlineHelpLauncher.TryOpenAsync(url)");
            StringAssert.Contains(source, "if (showHelpIcon)");
            StringAssert.Contains(source, "menuItem.Icon = CreateThemedIcon(\"HelpImage\")");
            Assert.IsFalse(source.Contains("CreateThemedIcon(\"HelpIcon\")", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("Icon = TryFindResource(\"HelpIcon\")", StringComparison.Ordinal));
            StringAssert.Contains(source, "private static MenuItem CreateHelpMenuSeparator()");
            StringAssert.Contains(source, "separatorLine.SetResourceReference(Border.BackgroundProperty, \"MenuPopupDefaultSeparator\")");
            StringAssert.Contains(source, "await OpenOnlineResourceAsync(\"citation information\", OnlineHelpLauncher.ZenodoConceptDoiUrl)");
            StringAssert.Contains(source, "hyperlink.IsEnabled = false");
            StringAssert.Contains(source, "hyperlink.IsEnabled = true");
            StringAssert.Contains(source, "IsHitTestVisible = false");
            StringAssert.Contains(source, "menuItem.IsEnabled = false;");
            StringAssert.Contains(source, "menuItem.IsEnabled = true;");
            StringAssert.Contains(source, "No internet connection is available. Please check your connection and try again.");
            StringAssert.Contains(source, "Something went wrong. Cannot open the RMC-BestFit ");
            Assert.IsTrue(source.IndexOf(userGuideCall, StringComparison.Ordinal) < source.IndexOf(technicalReferenceCall, StringComparison.Ordinal));
            Assert.IsTrue(source.IndexOf(technicalReferenceCall, StringComparison.Ordinal) < source.IndexOf(exampleProjectsCall, StringComparison.Ordinal));
            Assert.IsTrue(source.IndexOf(exampleProjectsCall, StringComparison.Ordinal) < source.IndexOf(separatorCall, StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("Quick Start Guide", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains(".pdf", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verifies the framework Terms viewer enables hyperlink interaction while remaining read-only.
        /// </summary>
        /// <remarks>
        /// This exercises the actual framework window and named RichTextBox rather than relying only on a
        /// source-level integration assertion.
        /// </remarks>
        [STATestMethod]
        public void EnableTermsDocumentLinks_EnablesFrameworkRichTextBoxLinks()
        {
            var window = new FrameworkUI.TermsAndConditionsWindow();
            System.Windows.Controls.RichTextBox termsTextBox =
                window.FindName("TCURichTextBox") as System.Windows.Controls.RichTextBox;
            System.Reflection.MethodInfo enableLinks = typeof(RMC_BestFit.MainProjectNode).GetMethod(
                "EnableTermsDocumentLinks",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            Assert.IsNotNull(termsTextBox);
            Assert.IsTrue(termsTextBox.IsReadOnly);
            Assert.IsFalse(termsTextBox.IsDocumentEnabled);
            Assert.IsNotNull(enableLinks);

            enableLinks.Invoke(null, new object[] { window });

            Assert.IsTrue(termsTextBox.IsDocumentEnabled);
            Assert.IsTrue(termsTextBox.IsReadOnly);
        }

        /// <summary>
        /// Verifies both linked documentation indexes display active-development note boxes.
        /// </summary>
        /// <remarks>
        /// Keeping the warnings directly beneath each title ensures GitHub renders them before the document content.
        /// </remarks>
        [TestMethod]
        public void DocumentationIndexes_DeclareActiveDevelopment()
        {
            string technicalReference = ReadRepositoryFile("docs/index.md").Replace("\r\n", "\n");
            string exampleProjects = ReadRepositoryFile("examples/README.md").Replace("\r\n", "\n");

            const string technicalReferenceOpening =
                "# RMC-BestFit Library Documentation\n\n" +
                "> [!NOTE]\n" +
                "> This technical reference is under active development for RMC-BestFit 2.0. Content may be incomplete or change as the software and documentation are finalized.\n";
            const string exampleProjectsOpening =
                "# RMC-BestFit Example Projects\n\n" +
                "> [!NOTE]\n" +
                "> These example projects and tutorials are under active development for RMC-BestFit 2.0. Workflows, screenshots, output tables, and expected results may be incomplete or change as the examples are finalized.\n";

            Assert.IsTrue(technicalReference.StartsWith(technicalReferenceOpening, StringComparison.Ordinal));
            Assert.IsTrue(exampleProjects.StartsWith(exampleProjectsOpening, StringComparison.Ordinal));
        }

        /// <summary>
        /// Verifies the README Documentation section identifies the version 2.0 materials under active development.
        /// </summary>
        /// <remarks>
        /// The exact section opening keeps the callout immediately below its heading and prevents an em dash from
        /// returning to the approved wording.
        /// </remarks>
        [TestMethod]
        public void ReadmeDocumentation_DeclaresActiveDevelopmentWithoutEmDash()
        {
            string readme = ReadRepositoryFile("README.md").Replace("\r\n", "\n");
            const string note =
                "> [!NOTE]\n" +
                "> Documentation for RMC-BestFit 2.0, including the User Guide, Technical Reference, Example Projects, and verification materials, is under active development and may be incomplete or change. The Version 1.0 User's Guide and Verification Report linked below remain published references for the previous major release.";
            const string expectedSectionOpening =
                "## Documentation\n\n" +
                note +
                "\n\n| Document | Description |";

            StringAssert.Contains(readme, expectedSectionOpening);
            Assert.IsFalse(note.Contains('\u2014'));
        }

        /// <summary>
        /// Reads a repository file relative to the project root.
        /// </summary>
        /// <param name="relativePath">Repository-relative path of the file to read.</param>
        /// <param name="sourceFilePath">The compiler-provided path to this test source file.</param>
        /// <returns>The requested file's source text.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="relativePath"/> is empty.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the compiler-provided source directory cannot be determined.
        /// </exception>
        /// <exception cref="IOException">Thrown when the requested repository file cannot be read.</exception>
        /// <remarks>
        /// The repository root is resolved relative to the checked-in test source so tests do not depend
        /// on the process working directory or build-output layout.
        /// </remarks>
        private static string ReadRepositoryFile(
            string relativePath,
            [CallerFilePath] string sourceFilePath = "")
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new ArgumentException("A repository-relative path is required.", nameof(relativePath));
            }

            string sourceDirectory = Path.GetDirectoryName(sourceFilePath);
            if (string.IsNullOrEmpty(sourceDirectory))
            {
                throw new InvalidOperationException("Could not determine the test source directory.");
            }

            string repositoryRoot = Path.GetFullPath(Path.Combine(
                sourceDirectory,
                "..",
                "..",
                "..",
                ".."));

            return File.ReadAllText(Path.Combine(repositoryRoot, relativePath));
        }
    }
}
