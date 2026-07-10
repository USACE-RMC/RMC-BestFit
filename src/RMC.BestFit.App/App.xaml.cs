using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shell;
using System.Windows.Threading;
using SoftwareUpdate;
using SoftwareUpdate.GitHub;

namespace RMC_BestFit
{
    /// <summary>
    /// Main application class for the RMC-BestFit WPF application. Handles application initialization,
    /// startup procedures, exception handling, and Windows Jump List management.
    /// </summary>
    public partial class App : Application
    {
        internal const string ProductVersion = "2.0-beta.5";
        internal const string ProductVersionDate = "July 2026";

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class. Configures the Windows Jump List
        /// to hide recent items for improved user privacy and cleaner task bar integration.
        /// </summary>
        public App()
        {
            // Register assembly resolver to load DLLs from the Libraries subfolder.
            // This must be done before any external assemblies are referenced.
            AssemblyResolver.Register();

            var jumpList = new JumpList();
            jumpList.ShowRecentCategory = false;
            jumpList.Apply();
            JumpList.SetJumpList(Application.Current, jumpList);
        }

        /// <summary>
        /// Handles the application startup event. Initializes software version information, creates the project model,
        /// configures version compatibility handlers, creates the main window, and processes command line arguments
        /// to open project files if specified.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="StartupEventArgs"/> instance containing the event data, including command line arguments.</param>
        /// <remarks>
        /// This method performs the following initialization tasks:
        /// <list type="bullet">
        /// <item>Sets software version and extension information</item>
        /// <item>Creates and configures the project model instance</item>
        /// <item>Registers a handler for opening version 1.0 projects with appropriate warnings</item>
        /// <item>Creates a temporary dummy project for initialization</item>
        /// <item>Ensures the user settings folder exists</item>
        /// <item>Creates and displays the main window</item>
        /// <item>Processes command line arguments to open specified project files</item>
        /// </list>
        /// Command line arguments are handled to support opening projects from Windows Explorer or the Jump List.
        /// </remarks>
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Initialize theme system - must be done before creating any UI
            FrameworkUI.ThemeManager.SetTheme(FrameworkUI.ThemeColor.Light);

            FrameworkUI.ShellPublicVariables.SoftwareVersionDate = ProductVersionDate;
            FrameworkUI.ShellPublicVariables.SoftwareVersion = ProductVersion;
            FrameworkUI.ShellPublicVariables.SoftwareExtension = ".bestfit";

            // Create the project model
            var project = RMC.BestFit.UI.BestFitProject.GetInstance();
            // Check if opening from version 1.0.
            project.OpenedVersion1 += (bool isVersion1, ref bool cancel ) =>
            {
                if (isVersion1 == true)
                {
                    if (GenericControls.MessageBox.Show("You are opening a project created with version 1.0 of RMC-BestFit. All analyses must be rerun with version 2.0. Note that any edits made to this project can prevent it from being editable in version 1.0.", "Warning!", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.Cancel)
                    {
                        cancel = true;
                    }
                }
            };

            // Create temp dummy project
            project.CreateNewDummyProject();

            // Create settings folder if it doesn't exist (used by event tree templates and styles).
            if (System.IO.Directory.Exists(FrameworkUI.ShellPublicVariables.UserSettingsFolderPath) == false)
                System.IO.Directory.CreateDirectory(FrameworkUI.ShellPublicVariables.UserSettingsFolderPath);

            // Create and Show the Main Window
            FrameworkUI.MainWindow mainWindow = new FrameworkUI.MainWindow();
            mainWindow.ProjectNode = new MainProjectNode(project) { Style = (Style)FindResource("TreeViewItemStyle") };

            // Sync theme warning suppression from persisted settings to OxyPlotThemeManager
            OxyPlotControls.OxyPlotThemeManager.SuppressThemeChangeWarning = FrameworkUI.UserSettings.SuppressThemeChangeWarning;

            // Sync back before UserSettings is saved to disk
            FrameworkUI.UserSettings.Saving += () =>
                FrameworkUI.UserSettings.SuppressThemeChangeWarning = OxyPlotControls.OxyPlotThemeManager.SuppressThemeChangeWarning;

            // Wire up the Check-for-Updates service so the Tools menu entry becomes visible.
            var updateOptions = CreateUpdateOptions();
            var updateService = new GitHubUpdateService(updateOptions);
            mainWindow.UpdateService = updateService;

            // Background auto-check 5 seconds after launch — silent unless an update is found.
            // Task is intentionally discarded; all failures are caught inside the method.
            _ = AutoCheckForUpdatesAsync(mainWindow, updateService, delayMs: 5000);

            // If the file is opened from the command line, then open from file
            if (e.Args != null && e.Args.Length >= 1)
            {

                // First get the full file name from the command line arguments
                string fullFileName = "";
                if (e.Args.Length == 1)
                    // CASE: User is opening a project from Windows Explorer
                    fullFileName = e.Args[0].ToString();
                else if (e.Args.Length > 1)
                    // CASE: User is opening from the Jump List
                    fullFileName = string.Join(" ", e.Args);

                // Next, open the project being called from command line.
                // Use EndsWith to avoid ArgumentOutOfRangeException when fullFileName is
                // shorter than the extension (e.g., a single-char argument).
                if (fullFileName.EndsWith(FrameworkUI.ShellPublicVariables.SoftwareExtension, StringComparison.OrdinalIgnoreCase))
                    mainWindow.OpenRecentProject(fullFileName, true);
            }

            mainWindow.Show();
        }

        /// <summary>
        /// Handles unhandled exceptions that occur on the dispatcher thread. Provides special handling for
        /// COM exceptions and creates backup files for unexpected errors when auto-backup is enabled.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DispatcherUnhandledExceptionEventArgs"/> instance containing the exception data.</param>
        /// <remarks>
        /// This method handles two specific scenarios:
        /// <list type="bullet">
        /// <item>COM Exception with error code -2147221040 (CLIPBRD_E_CANT_OPEN): The exception is marked as handled and suppressed.</item>
        /// <item>All other unhandled exceptions: If backup retention is enabled, a backup project file is automatically created before the application terminates.</item>
        /// </list>
        /// This provides a safety mechanism to preserve user work in the event of unexpected application crashes.
        /// </remarks>
        private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Always log the unhandled exception so bug-report submitters can capture the
            // detail from a crash dump or attached debugger. Without this, the user only
            // sees the auto-backup behavior with no exception text recorded anywhere.
            System.Diagnostics.Debug.WriteLine($"Application.DispatcherUnhandledException: {e.Exception}");

            var comException = e.Exception as System.Runtime.InteropServices.COMException;
            if (comException != null && comException.ErrorCode == -2147221040)
                e.Handled = true;
            else if (FrameworkUI.UserSettings.KeepLastBackupVersion == true)
                FrameworkUI.AutoBackup.CreateBackupProjectFile();
        }

        /// <summary>
        /// Resolves the current application version used by the update check. Prefers
        /// <see cref="AssemblyInformationalVersionAttribute"/> (which can carry pre-release
        /// suffixes such as "-beta.4"), falling back to the numeric <see cref="AssemblyName.Version"/>
        /// if the informational value is missing or malformed.
        /// </summary>
        /// <returns>A <see cref="SemanticVersion"/> representing the running application's version.</returns>
        /// <remarks>
        /// BestFit ships tagged GitHub releases of the form <c>v2.0-beta.N</c> and later <c>v2.0</c> /
        /// <c>v2.0.1</c>. A 4-part <c>AssemblyVersion</c> cannot express the pre-release suffix, so
        /// <c>AssemblyInformationalVersion</c> is the source of truth and is bumped per release.
        /// </remarks>
        internal static SemanticVersion ResolveCurrentVersion()
        {
            var asm = Assembly.GetExecutingAssembly();
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info) && SemanticVersion.TryParse(info, out var parsed) && parsed != null)
                return parsed;
            var v = asm.GetName().Version ?? new Version(2, 0, 0);
            return SemanticVersion.FromVersion(v);
        }

        /// <summary>
        /// Creates the application update configuration for the public RMC-BestFit release repository.
        /// </summary>
        /// <returns>The checksum-enforcing update options for the running application version.</returns>
        /// <remarks>
        /// Keeping release-sensitive updater values in one factory makes their consistency directly testable.
        /// </remarks>
        internal static UpdateOptions CreateUpdateOptions()
        {
            return new UpdateOptions
            {
                GitHubOwner = "USACE-RMC",
                GitHubRepo = "RMC-BestFit",
                CurrentVersion = ResolveCurrentVersion(),
                AssetNamePattern = "RMC-BestFit.*.zip",
                IncludePreReleases = true,
                CreateBackup = true,
                MainExecutableName = "RMC-BestFit.exe",
                RequireSha256Checksum = true
            };
        }

        /// <summary>
        /// Checks for application updates in the background after a startup delay. Prompts the
        /// user with Yes/No if a newer release is available and has not been previously skipped.
        /// </summary>
        /// <param name="mainWindow">The main application window (used as dialog owner and dispatcher host).</param>
        /// <param name="updateService">The configured update service.</param>
        /// <param name="delayMs">Milliseconds to wait after startup before running the check.</param>
        /// <remarks>
        /// Yes triggers the existing Tools-menu download/install flow by raising a click on the
        /// <c>CheckForUpdatesMenuItem</c> looked up through the window's name scope. No persists the
        /// version to the skipped-versions file so the user is not re-prompted on subsequent launches
        /// for the same version. All failures (network outage, rate limit, missing repo, parse error)
        /// are swallowed to a Debug line so launch is never disrupted.
        /// </remarks>
        private async Task AutoCheckForUpdatesAsync(FrameworkUI.MainWindow mainWindow,
                                                     IUpdateService updateService, int delayMs)
        {
            try
            {
                await Task.Delay(delayMs);
                var result = await updateService.CheckForUpdateAsync();
                if (!result.IsUpdateAvailable || result.Update == null) return;
                if (updateService.IsVersionSkipped(result.Update.Version)) return;

                await mainWindow.Dispatcher.InvokeAsync(() =>
                {
                    var newVer = result.Update.Version;
                    var msg = $"A new version ({newVer}) is available.\n\n" +
                              $"You are currently running version {updateService.Options.CurrentVersion}.\n\n" +
                              "Would you like to download and install the update now?\n" +
                              "(Select No to keep your current version \u2014 you won't be prompted about this version again.)";
                    var choice = GenericControls.MessageBox.Show(mainWindow, msg, "Update Available",
                        MessageBoxButton.YesNo, MessageBoxImage.Information);

                    if (choice == MessageBoxResult.Yes)
                    {
                        // Re-use the MainWindow's built-in Click handler (handles progress UI,
                        // download, checksum, backup, install + restart). CheckForUpdatesMenuItem
                        // is an internal field in FrameworkUI, so look it up via the name scope.
                        if (mainWindow.FindName("CheckForUpdatesMenuItem") is System.Windows.Controls.MenuItem mi)
                            mi.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));
                    }
                    else
                    {
                        updateService.SkipVersion(newVer);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-update check failed: {ex.Message}");
            }
        }

    }
}
