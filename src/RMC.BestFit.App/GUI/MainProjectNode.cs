using OxyPlot.Wpf;
using OxyPlotControls;
using GenericControls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RMC.BestFit.Models;
using RMC.BestFit.UI;
using FrameworkInterfaces;
using FrameworkUI;
using FrameworkUI.ProjectExplorer;
using System.Collections.ObjectModel;

namespace RMC_BestFit
{
    /// <summary>
    /// Main project node controller for the RMC-BestFit WPF application. Manages the project tree structure,
    /// document controls, menu items, and user interface interactions for all analysis types including
    /// time series, input data, distribution fitting, univariate, bivariate, rating curve, and time series analyses.
    /// </summary>
    /// <remarks>
    /// This class extends <see cref="FrameworkUIController"/> and serves as the central coordinator for:
    /// <list type="bullet">
    /// <item>Project explorer context menu definitions</item>
    /// <item>Main window project menu definitions</item>
    /// <item>AvalonDock document and properties control management</item>
    /// <item>OxyPlot properties panel coordination</item>
    /// <item>Creation wizards for all analysis and data element types</item>
    /// </list>
    /// </remarks>
    public class MainProjectNode : FrameworkUIController
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MainProjectNode"/> class with the specified project.
        /// Configures the plot properties control and registers event handlers for property panel interactions.
        /// </summary>
        /// <param name="project">The project instance to associate with this node controller.</param>
        public MainProjectNode(IProject project) : base(project)
        {
            _plotPropertiesControl = new OxyPlotPropertiesControl() { Margin = new Thickness(0, 0, 5, 5) };
            _plotPropertiesControl.ClosePropertiesCalled += (x) => { ClosePlotProperties_Click(x.Plot); };
        }

        /// <summary>
        /// Indicates whether the plot properties control panel is currently open and visible to the user.
        /// </summary>
        private bool _plotPropertiesOpen;

        /// <summary>
        /// Flag indicating that the plot properties control is in the process of closing.
        /// Used to prevent recursive closure operations and ensure proper cleanup.
        /// </summary>
        private bool _plotPropertiesClosing = false;

        /// <summary>
        /// The OxyPlot properties control instance used to display and edit plot configuration settings.
        /// This control is shared across all plot types in the application.
        /// </summary>
        private OxyPlotPropertiesControl _plotPropertiesControl;

        /// <summary>Gets a value indicating that the project node does not support multi-select.</summary>
        public override bool CanMultiSelect => false;

        #region Menu Items

        /// <summary>
        /// Defines and configures context menu items for the project explorer tree view. Creates custom menu items
        /// for each element collection type including time series, input data, and various analysis types.
        /// </summary>
        /// <remarks>
        /// This method iterates through all child nodes in the project tree and adds appropriate context menu items
        /// based on the element collection type. Menu items include creation wizards for new elements and batch
        /// run operations for analysis collections. The method disables the default "Create New" context item and
        /// replaces it with custom, type-specific menu items.
        /// </remarks>
        protected override void DefineProjectExplorerMenuItems()
        {
            for (int i = 0; i < ChildNodes.Count; i++)
            {
                ElementNodeCollection elementNodeCollection = null;
                if (ChildNodes[i] as ElementNodeCollection != null)
                    elementNodeCollection = (ElementNodeCollection)ChildNodes[i];

                if (elementNodeCollection == null) continue;

                // Time Series Data
                if (elementNodeCollection.ElementCollection.GetType() == typeof(TimeSeriesCollection))
                {
                    MenuItem menuItem = new MenuItem() { Header = "New Time Series...", Icon = CreateThemedIcon("TimeSeriesDataIcon") };
                    menuItem.Click += (object sender, RoutedEventArgs e) => CreateNewTimeSeriesElement_Click(elementNodeCollection.ElementCollection);
                    elementNodeCollection.CustomContextItems.Add(menuItem);
                }

                // Input Data
                if (elementNodeCollection.ElementCollection.GetType() == typeof(InputDataCollection))
                {
                    MenuItem menuItem = new MenuItem() { Header = "New Input Data...", Icon = CreateThemedIcon("InputDataIcon") };
                    menuItem.Click += (object sender, RoutedEventArgs e) => CreateNewInputData_Click(elementNodeCollection.ElementCollection);
                    elementNodeCollection.CustomContextItems.Add(menuItem);
                }

                // Distribution Fitting Analysis
                if (elementNodeCollection.ElementCollection.GetType() == typeof(FittingAnalysisCollection))
                {
                    MenuItem menuItem = new MenuItem() { Header = "New Distribution Fitting Analysis...", Icon = CreateThemedIcon("FittingAnalysisIcon") };
                    menuItem.Click += (object sender, RoutedEventArgs e) => CreateNewFittingAnalysis_Click(elementNodeCollection.ElementCollection);
                    elementNodeCollection.CustomContextItems.Add(menuItem);
                }

                // Univariate Distribution Analysis
                if (elementNodeCollection.ElementCollection.GetType() == typeof(UnivariateAnalysisCollection))
                {
                    MenuItem udaMI = new MenuItem() { Header = "New Univariate Distribution Analysis...", Icon = CreateThemedIcon("UnivariateAnalysisIcon") };
                    udaMI.Click += (object sender, RoutedEventArgs e) => CreateNewUnivariateAnalysis_Click(elementNodeCollection.ElementCollection);
                    elementNodeCollection.CustomContextItems.Add(udaMI);

                    MenuItem b17MI = new MenuItem() { Header = "New Bulletin 17C Analysis...", Icon = CreateThemedIcon("B17AnalysisIcon") };
                    b17MI.Click += (object sender, RoutedEventArgs e) => CreateNewB17CAnalysis_Click(elementNodeCollection.ElementCollection);
                    elementNodeCollection.CustomContextItems.Add(b17MI);

                    MenuItem ppaMI = new MenuItem() { Header = "New Point Process Analysis...", Icon = CreateThemedIcon("PointProcessAnalysisIcon") };
                    ppaMI.Click += (object sender, RoutedEventArgs e) => CreateNewPointProcessAnalysis_Click(elementNodeCollection.ElementCollection);
                    elementNodeCollection.CustomContextItems.Add(ppaMI);

                    MenuItem mdaMI = new MenuItem() { Header = "New Mixture Distribution Analysis...", Icon = CreateThemedIcon("MixtureAnalysisIcon") };
                    mdaMI.Click += (object sender, RoutedEventArgs e) => CreateNewMixtureAnalysis_Click(elementNodeCollection.ElementCollection);
                    elementNodeCollection.CustomContextItems.Add(mdaMI);

                    MenuItem cudaMI = new MenuItem() { Header = "New Composite Distribution Analysis...", Icon = CreateThemedIcon("CompositeAnalysisIcon") };
                    cudaMI.Click += (object sender, RoutedEventArgs e) => CreateNewCompositeAnalysis_Click(elementNodeCollection.ElementCollection);
                    elementNodeCollection.CustomContextItems.Add(cudaMI);

                    // Batch Run
                    MenuItem batchMI = new MenuItem() { Header = "Batch Run...", Icon = CreateThemedIcon("BatchRunIcon") };
                    batchMI.Click += (object sender, RoutedEventArgs e) =>
                    {
                        var batchWindow = new BatchRunWindow();
                        // Create list of valid analyses.
                        // Composite analyses are included if they are batch-eligible (properly
                        // configured but component analyses not yet estimated) since the batch
                        // runner will estimate their dependencies first.
                        List<IElement> list = new List<IElement>();
                        foreach (var analysis in elementNodeCollection.ElementCollection)
                        {
                            if (analysis.IsValid)
                                list.Add(analysis);
                            else if (analysis is CompositeAnalysis ca && ca.IsBatchEligible)
                                list.Add(analysis);
                        }
                        batchWindow.Analyses = new ObservableCollection<IElement>(list);
                        batchWindow.Owner = Window.GetWindow(this);
                        batchWindow.Show();
                    };
                    elementNodeCollection.CustomContextItems.Add(batchMI);

                }

                // Bivariate Distribution Analysis
                if (elementNodeCollection.ElementCollection.GetType() == typeof(BivariateAnalysisCollection))
                {
                    MenuItem menuItem = new MenuItem() { Header = "New Bivariate Distribution Analysis...", Icon = CreateThemedIcon("BivariateAnalysisIcon") };
                    menuItem.Click += (object sender, RoutedEventArgs e) => CreateNewBivariateAnalysis_Click(elementNodeCollection.ElementCollection);
                    elementNodeCollection.CustomContextItems.Add(menuItem);

                    MenuItem cfaMI = new MenuItem() { Header = "New Coincident Frequency Analysis...", Icon = CreateThemedIcon("CoincidentFrequencyAnalysisIcon") };
                    cfaMI.Click += (object sender, RoutedEventArgs e) => CreateNewCoincidentFrequencyAnalysis_Click(elementNodeCollection.ElementCollection);
                    elementNodeCollection.CustomContextItems.Add(cfaMI);

                    // Batch Run
                    MenuItem batchMI = new MenuItem() { Header = "Batch Run...", Icon = CreateThemedIcon("BatchRunIcon") };
                    batchMI.Click += (object sender, RoutedEventArgs e) =>
                    {
                        var batchWindow = new BatchRunWindow();
                        // Create list of valid analyses. CoincidentFrequencyAnalysis is included
                        // when batch-eligible (configured but upstream BA not yet estimated) since
                        // the model-layer batch runner orders CFAs into Phase 3 — the un-estimated
                        // upstream BivariateAnalysis in the same collection is fitted in Phase 1
                        // first. Mirrors the univariate handler above (Composite + components in
                        // UnivariateAnalysisCollection); the iteration walks every sibling element
                        // in the collection so dependents are loaded automatically via the IsValid
                        // branch — no separate dependent-discovery pass is needed.
                        List<IElement> list = new List<IElement>();
                        foreach (var analysis in elementNodeCollection.ElementCollection)
                        {
                            if (analysis.IsValid == true)
                                list.Add(analysis);
                            else if (analysis is CoincidentFrequencyAnalysis cfa && cfa.IsBatchEligible)
                                list.Add(analysis);
                        }
                        batchWindow.Analyses = new ObservableCollection<IElement>(list);
                        batchWindow.Owner = Window.GetWindow(this);
                        batchWindow.Show();
                    };
                    elementNodeCollection.CustomContextItems.Add(batchMI);
                }

                // Rating Curve Analysis
                if (elementNodeCollection.ElementCollection.GetType() == typeof(RatingCurveAnalysisCollection))
                {
                    MenuItem menuItem = new MenuItem() { Header = "New Rating Curve Analysis...", Icon = CreateThemedIcon("RatingCurveAnalysisIcon") };
                    menuItem.Click += (object sender, RoutedEventArgs e) => CreateNewRatingCurveAnalysis_Click(elementNodeCollection.ElementCollection);
                    elementNodeCollection.CustomContextItems.Add(menuItem);

                    // Batch Run
                    MenuItem batchMI = new MenuItem() { Header = "Batch Run...", Icon = CreateThemedIcon("BatchRunIcon") };
                    batchMI.Click += (object sender, RoutedEventArgs e) =>
                    {
                        var batchWindow = new BatchRunWindow();
                        // Create list of valid analyses
                        List<IElement> list = new List<IElement>();
                        foreach (var analysis in elementNodeCollection.ElementCollection)
                        {
                            if (analysis.IsValid == true)
                                list.Add(analysis);
                        }
                        batchWindow.Analyses = new ObservableCollection<IElement>(list);
                        batchWindow.Owner = Window.GetWindow(this);
                        batchWindow.Show();
                    };
                    elementNodeCollection.CustomContextItems.Add(batchMI);
                }

                // Time Series Analysis
                if (elementNodeCollection.ElementCollection.GetType() == typeof(TimeSeriesAnalysisCollection))
                {
                    MenuItem menuItem = new MenuItem() { Header = "New Time Series Analysis...", Icon = CreateThemedIcon("TimeSeriesAnalysisIcon") };
                    menuItem.Click += (object sender, RoutedEventArgs e) => CreateNewTimeSeriesAnalysis_Click(elementNodeCollection.ElementCollection);
                    elementNodeCollection.CustomContextItems.Add(menuItem);

                    // Batch Run
                    MenuItem batchMI = new MenuItem() { Header = "Batch Run...", Icon = CreateThemedIcon("BatchRunIcon") };
                    batchMI.Click += (object sender, RoutedEventArgs e) =>
                    {
                        var batchWindow = new BatchRunWindow();
                        // Create list of valid analyses
                        List<IElement> list = new List<IElement>();
                        foreach (var analysis in elementNodeCollection.ElementCollection)
                        {
                            if (analysis.IsValid == true)
                                list.Add(analysis);
                        }
                        batchWindow.Analyses = new ObservableCollection<IElement>(list);
                        batchWindow.Owner = Window.GetWindow(this);
                        batchWindow.Show();
                    };
                    elementNodeCollection.CustomContextItems.Add(batchMI);
                }

            }
        }

        /// <summary>
        /// Defines and configures menu items for the main window's Project menu. Creates menu entries for
        /// adding new data elements and analyses, organized by type and functionality.
        /// </summary>
        /// <remarks>
        /// This method builds the Project menu by iterating through all element collections in the project
        /// and creating appropriate menu items for each type. For univariate analyses, multiple sub-menu
        /// items are grouped under a single parent menu item. All menu items are added to the
        /// _projectMenuItems collection for display in the main window.
        /// </remarks>
        protected override void DefineProjectMenuItems()
        {
            for (int i = 0; i < Project.ElementCollections.Count; i++)
            {
                var collection = Project.ElementCollections[i];

                // Time Series Data
                if (collection.GetType() == typeof(TimeSeriesCollection))
                {
                    MenuItem menuItem = new MenuItem() { Header = "New Time Series...", Icon = CreateThemedIcon("TimeSeriesDataIcon") };
                    menuItem.Click += (object sender, RoutedEventArgs e) => CreateNewTimeSeriesElement_Click(collection);
                    _projectMenuItems.Add(menuItem);
                }

                // Input Data
                if (collection.GetType() == typeof(InputDataCollection))
                {
                    MenuItem menuItem = new MenuItem() { Header = "New Input Data...", Icon = CreateThemedIcon("InputDataIcon") };
                    menuItem.Click += (object sender, RoutedEventArgs e) => CreateNewInputData_Click(collection);
                    _projectMenuItems.Add(menuItem);
                }

                // Distribution Fitting Analysis
                if (collection.GetType() == typeof(FittingAnalysisCollection))
                {
                    MenuItem menuItem = new MenuItem() { Header = "New Distribution Fitting Analysis...", Icon = CreateThemedIcon("FittingAnalysisIcon") };
                    menuItem.Click += (object sender, RoutedEventArgs e) => CreateNewFittingAnalysis_Click(collection);
                    _projectMenuItems.Add(menuItem);
                }

                // Univariate Distribution Analysis
                if (collection.GetType() == typeof(UnivariateAnalysisCollection))
                {
                    MenuItem udaMI = new MenuItem() { Header = "New Univariate Distribution Analysis...", Icon = CreateThemedIcon("UnivariateAnalysisIcon") };
                    udaMI.Click += (object sender, RoutedEventArgs e) => CreateNewUnivariateAnalysis_Click(collection);

                    MenuItem b17MI = new MenuItem() { Header = "New Bulletin 17C Analysis...", Icon = CreateThemedIcon("B17AnalysisIcon") };
                    b17MI.Click += (object sender, RoutedEventArgs e) => CreateNewB17CAnalysis_Click(collection);

                    MenuItem ppaMI = new MenuItem() { Header = "New Point Process Analysis...", Icon = CreateThemedIcon("PointProcessAnalysisIcon") };
                    ppaMI.Click += (object sender, RoutedEventArgs e) => CreateNewPointProcessAnalysis_Click(collection);

                    MenuItem mdaMI = new MenuItem() { Header = "New Mixture Distribution Analysis...", Icon = CreateThemedIcon("MixtureAnalysisIcon") };
                    mdaMI.Click += (object sender, RoutedEventArgs e) => CreateNewMixtureAnalysis_Click(collection);

                    MenuItem cudaMI = new MenuItem() { Header = "New Composite Distribution Analysis...", Icon = CreateThemedIcon("CompositeAnalysisIcon") };
                    cudaMI.Click += (object sender, RoutedEventArgs e) => CreateNewCompositeAnalysis_Click(collection);

                    MenuItem menuItem = new MenuItem() { Header = "Univariate Analyses...", Icon = CreateThemedIcon("UnivariateAnalysisIcon") };
                    menuItem.Items.Add(udaMI);
                    menuItem.Items.Add(b17MI);
                    menuItem.Items.Add(ppaMI);
                    menuItem.Items.Add(mdaMI);
                    menuItem.Items.Add(cudaMI);
                    _projectMenuItems.Add(menuItem);
                }

                // Bivariate Distribution Analysis
                if (collection.GetType() == typeof(BivariateAnalysisCollection))
                {
                    MenuItem menuItem = new MenuItem() { Header = "New Bivariate Distribution Analysis...", Icon = CreateThemedIcon("BivariateAnalysisIcon") };
                    menuItem.Click += (object sender, RoutedEventArgs e) => CreateNewBivariateAnalysis_Click(collection);
                    _projectMenuItems.Add(menuItem);

                    MenuItem cfaMI = new MenuItem() { Header = "New Coincident Frequency Analysis...", Icon = CreateThemedIcon("CoincidentFrequencyAnalysisIcon") };
                    cfaMI.Click += (object sender, RoutedEventArgs e) => CreateNewCoincidentFrequencyAnalysis_Click(collection);
                    _projectMenuItems.Add(cfaMI);
                }

                // Rating Curve Analysis
                if (collection.GetType() == typeof(RatingCurveAnalysisCollection))
                {
                    MenuItem menuItem = new MenuItem() { Header = "New Rating Curve Analysis...", Icon = CreateThemedIcon("RatingCurveAnalysisIcon") };
                    menuItem.Click += (object sender, RoutedEventArgs e) => CreateNewRatingCurveAnalysis_Click(collection);
                    _projectMenuItems.Add(menuItem);
                }

                // Time Series Analysis
                if (collection.GetType() == typeof(TimeSeriesAnalysisCollection))
                {
                    MenuItem menuItem = new MenuItem() { Header = "New Time Series Analysis...", Icon = CreateThemedIcon("TimeSeriesAnalysisIcon") };
                    menuItem.Click += (object sender, RoutedEventArgs e) => CreateNewTimeSeriesAnalysis_Click(collection);
                    _projectMenuItems.Add(menuItem);
                }

            } 
                
        }

        /// <summary>
        /// Defines custom tool menu items for the main window's Tools menu.
        /// Currently not implemented and returns immediately without adding any menu items.
        /// </summary>
        /// <remarks>
        /// This method is provided as an override point for future extensibility. Custom tools
        /// and utilities can be added to the Tools menu by implementing this method.
        /// </remarks>
        protected override void DefineToolsMenuItems()
        {
            return;
        }

        /// <summary>
        /// Defines and configures menu items for the main window's Help menu. Adds menu entries for
        /// accessing online documentation, example projects, Terms and Conditions, and the About dialog.
        /// </summary>
        /// <remarks>
        /// This method creates five help menu items:
        /// <list type="bullet">
        /// <item>User Guide - Opens the online RMC-BestFit user guide in the default browser</item>
        /// <item>Technical Reference - Opens the main-branch technical reference on GitHub</item>
        /// <item>Example Projects - Opens the main-branch example project collection on GitHub</item>
        /// <item>Terms and Conditions for Use - Displays the software license and usage terms dialog</item>
        /// <item>About RMC-BestFit - Shows version information and software credits</item>
        /// </list>
        /// A separator groups the three online resources above the application-specific items.
        /// All menu items are configured with appropriate icons and event handlers.
        /// </remarks>
        protected override void DefineHelpMenuItems()
        {
            _helpMenuItems.Add(CreateOnlineHelpMenuItem("User Guide", OnlineHelpLauncher.UserGuideUrl, showHelpIcon: true));
            _helpMenuItems.Add(CreateOnlineHelpMenuItem("Technical Reference", OnlineHelpLauncher.TechnicalReferenceUrl));
            _helpMenuItems.Add(CreateOnlineHelpMenuItem("Example Projects", OnlineHelpLauncher.ExampleProjectsUrl));
            _helpMenuItems.Add(CreateHelpMenuSeparator());

            // Terms & Conditions for Use
            var tcuMenuItem = new MenuItem() { Header = "Terms & Conditions for Use", Icon = TryFindResource("TCUIcon") };
            tcuMenuItem.Click += (s, e) =>
            {
                var window = new TermsAndConditionsWindow();
                window.ShowButtons = false;
                if (Application.Current?.MainWindow != null)
                {
                    window.Owner = Application.Current.MainWindow;
                    window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }
                window.ShowDialog();
            };
            _helpMenuItems.Add(tcuMenuItem);

            // About
            var aboutMenuItem = new MenuItem() { Header = "About " + ApplicationAttributes.Title, Icon = TryFindResource("AboutIcon") };
            aboutMenuItem.Click += (s, e) =>
            {
                var window = new AboutWindow();
                window.SoftwareImage = Application.Current.TryFindResource("BestFit") as ImageSource;
                if (Application.Current?.MainWindow != null)
                {
                    window.Owner = Application.Current.MainWindow;
                    window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }
                window.ShowDialog();
            };
            _helpMenuItems.Add(aboutMenuItem);

        }

        /// <summary>
        /// Creates a Help menu item that opens an online RMC-BestFit resource in the default browser.
        /// </summary>
        /// <param name="header">User-visible menu item header and resource name.</param>
        /// <param name="url">Absolute HTTPS URL of the resource.</param>
        /// <param name="showHelpIcon">Whether to display the Help icon next to the menu item.</param>
        /// <returns>A configured Help menu item.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="header"/> or <paramref name="url"/> is empty.
        /// </exception>
        /// <remarks>
        /// The item is disabled while connectivity is checked to prevent duplicate launches. Expected
        /// offline results and unexpected launch failures are reported with distinct user messages.
        /// </remarks>
        private MenuItem CreateOnlineHelpMenuItem(string header, string url, bool showHelpIcon = false)
        {
            if (string.IsNullOrWhiteSpace(header))
            {
                throw new ArgumentException("A Help menu header is required.", nameof(header));
            }
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("A Help resource URL is required.", nameof(url));
            }

            var menuItem = new MenuItem() { Header = header };
            if (showHelpIcon)
            {
                menuItem.Icon = CreateThemedIcon("HelpIcon");
            }

            menuItem.Click += async (x, y) =>
            {
                menuItem.IsEnabled = false;
                try
                {
                    OnlineHelpLaunchResult result = await OnlineHelpLauncher.TryOpenAsync(url);
                    if (result == OnlineHelpLaunchResult.NoInternet)
                    {
                        GenericControls.MessageBox.Show(
                            "No internet connection is available. Please check your connection and try again.",
                            "Connection Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to open " + header + ": " + ex.Message);
                    GenericControls.MessageBox.Show(
                        "Something went wrong. Cannot open the RMC-BestFit " + header + ".",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                finally
                {
                    menuItem.IsEnabled = true;
                }
            };

            return menuItem;
        }

        /// <summary>
        /// Creates a non-interactive, theme-aware separator compatible with the framework's Help menu collection.
        /// </summary>
        /// <returns>A menu item whose control template renders a horizontal separator.</returns>
        /// <remarks>
        /// The shared framework exposes Help entries as <see cref="MenuItem"/> instances, so a native
        /// <see cref="Separator"/> cannot be added to the collection. This template preserves that contract
        /// while using the framework's dynamic separator brush and icon-gutter alignment.
        /// </remarks>
        private static MenuItem CreateHelpMenuSeparator()
        {
            var separatorLine = new FrameworkElementFactory(typeof(Border));
            separatorLine.SetValue(FrameworkElement.HeightProperty, 1d);
            separatorLine.SetValue(FrameworkElement.MarginProperty, new Thickness(28d, 4d, 4d, 4d));
            separatorLine.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
            separatorLine.SetResourceReference(Border.BackgroundProperty, "MenuPopupDefaultSeparator");

            return new MenuItem
            {
                Focusable = false,
                IsEnabled = false,
                IsHitTestVisible = false,
                Template = new ControlTemplate(typeof(MenuItem))
                {
                    VisualTree = separatorLine
                }
            };
        }

        /// <summary>
        /// Handles the creation of a new time series data element. Displays a naming dialog and adds the element
        /// to the collection if a valid name is provided.
        /// </summary>
        /// <param name="collection">The element collection to which the new time series element will be added.</param>
        /// <remarks>
        /// The method generates a default name based on the current collection count and prompts the user
        /// to provide a unique name. If the user cancels or provides an empty name, no element is created.
        /// </remarks>
        private void CreateNewTimeSeriesElement_Click(IElementCollection collection)
        {
            string newName = CreateNewNameDialog("New Time Series", $"Time Series_{collection.Count + 1}", collection.Select(x => x.Name.ToString()).ToList());
            if (newName != "") collection.Add(new TimeSeriesElement(newName, collection));
        }

        /// <summary>
        /// Handles the creation of a new input data element. Displays a naming dialog and adds the element
        /// to the collection if a valid name is provided.
        /// </summary>
        /// <param name="collection">The element collection to which the new input data element will be added.</param>
        /// <remarks>
        /// The method generates a default name based on the current collection count and prompts the user
        /// to provide a unique name. If the user cancels or provides an empty name, no element is created.
        /// </remarks>
        private void CreateNewInputData_Click(IElementCollection collection)
        {
            string newName = CreateNewNameDialog("New Input Data", $"Input Data_{collection.Count + 1}", collection.Select(x => x.Name.ToString()).ToList());
            if (newName != "") collection.Add(new InputData(newName, collection));
        }

        /// <summary>
        /// Handles the creation of a new distribution fitting analysis. Displays a naming dialog and adds the
        /// analysis to the collection if a valid name is provided.
        /// </summary>
        /// <param name="collection">The element collection to which the new fitting analysis will be added.</param>
        /// <remarks>
        /// Distribution fitting analyses are used to fit statistical distributions to empirical data and evaluate
        /// goodness-of-fit. The method generates a default name and prompts for user confirmation.
        /// </remarks>
        private void CreateNewFittingAnalysis_Click(IElementCollection collection)
        {
            string newName = CreateNewNameDialog("New Distribution Fitting Analysis", $"Fitting Analysis_{collection.Count + 1}", collection.Select(x => x.Name.ToString()).ToList());
            if (newName != "") collection.Add(new FittingAnalysis(newName, collection));
        }

        /// <summary>
        /// Handles the creation of a new univariate distribution analysis. Displays a naming dialog and adds the
        /// analysis to the collection if a valid name is provided.
        /// </summary>
        /// <param name="collection">The element collection to which the new univariate analysis will be added.</param>
        /// <remarks>
        /// Univariate analyses use Bayesian methods to estimate single-variable probability distributions.
        /// The method generates a default name based on the collection count and validates uniqueness.
        /// </remarks>
        private void CreateNewUnivariateAnalysis_Click(IElementCollection collection)
        {
            string newName = CreateNewNameDialog("New Univariate Distribution Analysis", $"Univariate Analysis_{collection.Count + 1}", collection.Select(x => x.Name.ToString()).ToList());
            if (newName != "") collection.Add(new UnivariateAnalysis(newName, collection));
        }

        /// <summary>
        /// Handles the creation of a new point process analysis. Displays a naming dialog and adds the
        /// analysis to the collection if a valid name is provided.
        /// </summary>
        /// <param name="collection">The element collection to which the new point process analysis will be added.</param>
        /// <remarks>
        /// Point process analyses model events occurring in time or space using statistical methods appropriate
        /// for count data and arrival processes. A default name is generated based on the collection count.
        /// </remarks>
        private void CreateNewPointProcessAnalysis_Click(IElementCollection collection)
        {
            string newName = CreateNewNameDialog("New Point Process Analysis", $"Point Process Analysis_{collection.Count + 1}", collection.Select(x => x.Name.ToString()).ToList());
            if (newName != "") collection.Add(new PointProcessAnalysis(newName, collection));
        }

        /// <summary>
        /// Handles the creation of a new mixture distribution analysis. Displays a naming dialog and adds the
        /// analysis to the collection if a valid name is provided.
        /// </summary>
        /// <param name="collection">The element collection to which the new mixture analysis will be added.</param>
        /// <remarks>
        /// Mixture distribution analyses combine multiple probability distributions to model complex data patterns
        /// with multiple modes or populations. The method prompts the user for a unique name.
        /// </remarks>
        private void CreateNewMixtureAnalysis_Click(IElementCollection collection)
        {
            string newName = CreateNewNameDialog("New Mixture Distribution Analysis", $"Mixture Analysis_{collection.Count + 1}", collection.Select(x => x.Name.ToString()).ToList());
            if (newName != "") collection.Add(new MixtureAnalysis(newName, collection));
        }

        /// <summary>
        /// Handles the creation of a new Bulletin 17C flood frequency analysis. Displays a naming dialog and adds the
        /// analysis to the collection if a valid name is provided.
        /// </summary>
        /// <param name="collection">The element collection to which the new B17C analysis will be added.</param>
        /// <remarks>
        /// Bulletin 17C analyses use the Generalized Method of Moments (GMM) for flood frequency estimation
        /// following USGS/FEMA guidelines, with configurable uncertainty quantification methods.
        /// </remarks>
        private void CreateNewB17CAnalysis_Click(IElementCollection collection)
        {
            string newName = CreateNewNameDialog("New Bulletin 17C Analysis", $"B17C Analysis_{collection.Count + 1}", collection.Select(x => x.Name.ToString()).ToList());
            if (newName != "") collection.Add(new B17CAnalysis(newName, collection));
        }

        /// <summary>
        /// Handles the creation of a new composite distribution analysis. Displays a naming dialog and adds the
        /// analysis to the collection if a valid name is provided.
        /// </summary>
        /// <param name="collection">The element collection to which the new composite analysis will be added.</param>
        /// <remarks>
        /// Composite distribution analyses combine multiple independent distributions or analyses into a single
        /// unified probability distribution, useful for integrating results from different data sources or methods.
        /// </remarks>
        private void CreateNewCompositeAnalysis_Click(IElementCollection collection)
        {
            string newName = CreateNewNameDialog("New Composite Distribution Analysis", $"Composite Analysis_{collection.Count + 1}", collection.Select(x => x.Name.ToString()).ToList());
            if (newName != "") collection.Add(new CompositeAnalysis(newName, collection));
        }

        /// <summary>
        /// Handles the creation of a new bivariate distribution analysis. Displays a naming dialog and adds the
        /// analysis to the collection if a valid name is provided.
        /// </summary>
        /// <param name="collection">The element collection to which the new bivariate analysis will be added.</param>
        /// <remarks>
        /// Bivariate analyses model the joint probability distribution of two correlated variables using
        /// copula functions and marginal distributions. The method ensures name uniqueness across the collection.
        /// </remarks>
        private void CreateNewBivariateAnalysis_Click(IElementCollection collection)
        {
            string newName = CreateNewNameDialog("New Bivariate Distribution Analysis", $"Bivariate Analysis_{collection.Count + 1}", collection.Select(x => x.Name.ToString()).ToList());
            if (newName != "") collection.Add(new BivariateAnalysis(newName, collection));
        }

        /// <summary>
        /// Handles the creation of a new coincident frequency analysis. Displays a naming
        /// dialog and adds the analysis to the parent BivariateAnalysisCollection.
        /// </summary>
        /// <param name="collection">The element collection to add the new analysis to.</param>
        /// <remarks>
        /// A coincident frequency analysis computes joint exceedance probabilities for two
        /// correlated random variables (e.g., river inflow and reservoir pool elevation) and
        /// depends on an upstream <see cref="BivariateAnalysis"/> element to supply the fitted
        /// bivariate distribution. The method validates name uniqueness within the collection.
        /// </remarks>
        private void CreateNewCoincidentFrequencyAnalysis_Click(IElementCollection collection)
        {
            string newName = CreateNewNameDialog("New Coincident Frequency Analysis", $"Coincident Frequency_{collection.Count + 1}", collection.Select(x => x.Name.ToString()).ToList());
            if (newName != "") collection.Add(new CoincidentFrequencyAnalysis(newName, collection));
        }

        /// <summary>
        /// Handles the creation of a new rating curve analysis. Displays a naming dialog and adds the
        /// analysis to the collection if a valid name is provided.
        /// </summary>
        /// <param name="collection">The element collection to which the new rating curve analysis will be added.</param>
        /// <remarks>
        /// Rating curve analyses establish relationships between stage (water level) and discharge (flow rate)
        /// using various regression and uncertainty quantification methods. A unique name is required.
        /// </remarks>
        private void CreateNewRatingCurveAnalysis_Click(IElementCollection collection)
        {
            string newName = CreateNewNameDialog("New Rating Curve Analysis", $"Rating Curve Analysis_{collection.Count + 1}", collection.Select(x => x.Name.ToString()).ToList());
            if (newName != "") collection.Add(new RatingCurveAnalysis(newName, collection));
        }

        /// <summary>
        /// Handles the creation of a new time series analysis. Displays a naming dialog and adds the
        /// analysis to the collection if a valid name is provided.
        /// </summary>
        /// <param name="collection">The element collection to which the new time series analysis will be added.</param>
        /// <remarks>
        /// Time series analyses model temporal data patterns including trends, seasonality, and autocorrelation
        /// using various statistical and machine learning methods. The method validates name uniqueness.
        /// </remarks>
        private void CreateNewTimeSeriesAnalysis_Click(IElementCollection collection)
        {
            string newName = CreateNewNameDialog("New Time Series Analysis", $"Time Series Analysis_{collection.Count + 1}", collection.Select(x => x.Name.ToString()).ToList());
            if (newName != "") collection.Add(new TimeSeriesAnalysis(newName, collection));
        }

        /// <summary>
        /// Creates and displays a dialog for entering a new element name with validation to ensure uniqueness
        /// and compliance with naming rules. Returns the entered name if accepted, or an empty string if cancelled.
        /// </summary>
        /// <param name="title">The title text to display in the dialog window header.</param>
        /// <param name="initialName">The default name to pre-populate in the dialog's text box.</param>
        /// <param name="existingElementNames">The list of existing element names used for uniqueness validation. The new name must not match any existing names.</param>
        /// <returns>
        /// The validated element name entered by the user if the dialog is accepted; otherwise, an empty string
        /// if the dialog is cancelled or closed without confirmation.
        /// </returns>
        /// <remarks>
        /// The dialog enforces standard naming constraints including character limits and invalid character
        /// restrictions. The maximum name length is 50 characters, and default invalid characters are excluded.
        /// </remarks>
        private string CreateNewNameDialog(string title, string initialName, List<string> existingElementNames)
        {
            var nameDialog = new GenericControls.NameDialog(50, "", false, existingElementNames.ToArray(), NameTextBox.GetDefaultInvalidCharacters())
            {
                Icon = Application.Current.TryFindResource("AddImage") as ImageSource,
                Title = title,
                Owner = Window.GetWindow(this),
                Text = initialName
            };
            
            if (nameDialog.ShowDialog() == true)
                return nameDialog.Text;
            else
                return "";
        }

        /// <summary>
        /// Creates an <see cref="Image"/> element whose <see cref="Image.Source"/> is bound to the
        /// specified resource key via <see cref="FrameworkElement.SetResourceReference"/>,
        /// so the icon updates automatically when the theme changes.
        /// </summary>
        /// <param name="resourceKey">The resource key for the icon (e.g., "InputDataIcon").</param>
        /// <returns>An <see cref="Image"/> with a dynamic resource binding for its source.</returns>
        private static Image CreateThemedIcon(string resourceKey)
        {
            var img = new Image();
            img.SetResourceReference(Image.SourceProperty, resourceKey);
            return img;
        }

        #endregion

        #region AvalonDock

        /// <summary>
        /// Creates and returns the appropriate document control for displaying and editing the specified element.
        /// Matches the element type to its corresponding control and configures event handlers for plot properties
        /// and preview interactions.
        /// </summary>
        /// <param name="element">The project element to create a document control for (e.g., time series, input data, analysis).</param>
        /// <returns>
        /// A <see cref="Control"/> instance configured for the specified element type, or null if the element type
        /// is not recognized or does not have a corresponding document control.
        /// </returns>
        /// <remarks>
        /// This method supports all element types in the RMC-BestFit application including:
        /// <list type="bullet">
        /// <item>Time Series elements</item>
        /// <item>Input Data elements</item>
        /// <item>Fitting Analysis elements</item>
        /// <item>Univariate, Point Process, Mixture, B17C, and Composite analyses</item>
        /// <item>Bivariate Analysis elements</item>
        /// <item>Rating Curve Analysis elements</item>
        /// <item>Time Series Analysis elements</item>
        /// </list>
        /// Each control is configured with appropriate event handlers for plot property editing and user interactions.
        /// </remarks>
        public override Control GetDocumentControl(IElement element)
        {
            // Time Series
            if (element.ParentCollection as TimeSeriesCollection != null)
            {
                if (element as TimeSeriesElement != null)
                {
                    var cntrl = new TimeSeriesControl() { Element = (TimeSeriesElement)element };
                    cntrl.PlotPropertiesCalled += PlotPropertiesCalled;
                    cntrl.PreviewControlClicked += DocumentControl_PreviewClicked;
                    return cntrl;
                }
            }

            // Input Data
            if (element.ParentCollection as InputDataCollection != null)
            {
                if (element as InputData != null)
                {
                    var cntrl = new InputDataControl() { Element = (InputData)element };
                    cntrl.PlotPropertiesCalled += PlotPropertiesCalled;
                    cntrl.PreviewControlClicked += DocumentControl_PreviewClicked;
                    return cntrl;
                }
            }

            // Fitting Analysis
            if (element.ParentCollection as FittingAnalysisCollection != null)
            {
                if (element as FittingAnalysis != null)
                {
                    var cntrl = new FittingAnalysisControl() { Element = (FittingAnalysis)element };
                    cntrl.PlotPropertiesCalled += PlotPropertiesCalled;
                    cntrl.PreviewControlClicked += DocumentControl_PreviewClicked;
                    return cntrl;
                }
            }

            // Univariate Analysis
            if (element.ParentCollection as UnivariateAnalysisCollection != null)
            {
                // Univariate
                if (element as UnivariateAnalysis != null)
                {
                    var cntrl = new UnivariateAnalysisControl() { Element = (UnivariateAnalysis)element };
                    cntrl.PlotPropertiesCalled += PlotPropertiesCalled;
                    cntrl.PreviewControlClicked += DocumentControl_PreviewClicked;
                    return cntrl;
                }
                // Bulletin 17C Analysis
                if (element as B17CAnalysis != null)
                {
                    var cntrl = new B17CAnalysisControl() { Element = (B17CAnalysis)element };
                    cntrl.PlotPropertiesCalled += PlotPropertiesCalled;
                    cntrl.PreviewControlClicked += DocumentControl_PreviewClicked;
                    return cntrl;
                }
                // Point Process
                if (element as PointProcessAnalysis != null)
                {
                    var cntrl = new PointProcessAnalysisControl() { Element = (PointProcessAnalysis)element };
                    cntrl.PlotPropertiesCalled += PlotPropertiesCalled;
                    cntrl.PreviewControlClicked += DocumentControl_PreviewClicked;
                    return cntrl;
                }
                // Mixture
                if (element as MixtureAnalysis != null)
                {
                    var cntrl = new MixtureAnalysisControl() { Element = (MixtureAnalysis)element };
                    cntrl.PlotPropertiesCalled += PlotPropertiesCalled;
                    cntrl.PreviewControlClicked += DocumentControl_PreviewClicked;
                    return cntrl;
                }
                // Composite
                if (element as CompositeAnalysis != null)
                {
                    var cntrl = new CompositeAnalysisControl() { Element = (CompositeAnalysis)element };
                    cntrl.PlotPropertiesCalled += PlotPropertiesCalled;
                    cntrl.PreviewControlClicked += DocumentControl_PreviewClicked;
                    return cntrl;
                }
            }

            // Bivariate Analysis
            if (element.ParentCollection as BivariateAnalysisCollection != null)
            {
                if (element as BivariateAnalysis != null)
                {
                    var cntrl = new BivariateAnalysisControl() { Element = (BivariateAnalysis)element };
                    cntrl.PlotPropertiesCalled += PlotPropertiesCalled;
                    cntrl.PreviewControlClicked += DocumentControl_PreviewClicked;
                    return cntrl;
                }
                // Coincident Frequency Analysis
                if (element as CoincidentFrequencyAnalysis != null)
                {
                    var cntrl = new CoincidentFrequencyControl() { Element = (CoincidentFrequencyAnalysis)element };
                    cntrl.PlotPropertiesCalled += PlotPropertiesCalled;
                    cntrl.PreviewControlClicked += DocumentControl_PreviewClicked;
                    return cntrl;
                }
            }

            // Rating Curve Analysis
            if (element.ParentCollection as RatingCurveAnalysisCollection != null)
            {
                if (element as RatingCurveAnalysis != null)
                {
                    var cntrl = new RatingCurveAnalysisControl() { Element = (RatingCurveAnalysis)element };
                    cntrl.PlotPropertiesCalled += PlotPropertiesCalled;
                    cntrl.PreviewControlClicked += DocumentControl_PreviewClicked;
                    return cntrl;
                }
            }

            // Time Series Analysis
            if (element.ParentCollection as TimeSeriesAnalysisCollection != null)
            {
                if (element as TimeSeriesAnalysis != null)
                {
                    var cntrl = new TimeSeriesAnalysisControl() { Element = (TimeSeriesAnalysis)element };
                    cntrl.PlotPropertiesCalled += PlotPropertiesCalled;
                    cntrl.PreviewControlClicked += DocumentControl_PreviewClicked;
                    return cntrl;
                }
            }

            return null;
        }

        /// <summary>
        /// Retrieves the project element associated with the specified control. Works with both document controls
        /// and properties controls to return the underlying data element being displayed or edited.
        /// </summary>
        /// <param name="control">The control to retrieve the associated element from.</param>
        /// <returns>
        /// The <see cref="IElement"/> instance associated with the control, or null if the control type
        /// is not recognized or does not have an associated element.
        /// </returns>
        /// <remarks>
        /// This method supports extraction of elements from all document controls and properties controls
        /// used in the application. It performs type checking on the control to determine which element
        /// property to access.
        /// </remarks>
        public override IElement GetControlElement(UIElement control)
        {
            // Check if document control
            // Time Series
            if (control as TimeSeriesControl != null)
                return ((TimeSeriesControl)control).Element;
            // Input Data
            if (control as InputDataControl != null)
                return ((InputDataControl)control).Element;
            // Fitting Analysis
            if (control as FittingAnalysisControl != null)
                return ((FittingAnalysisControl)control).Element;
            // Univariate Analysis
            if (control as UnivariateAnalysisControl != null)
                return ((UnivariateAnalysisControl)control).Element;
            // Bulletin 17C Analysis
            if (control as B17CAnalysisControl != null)
                return ((B17CAnalysisControl)control).Element;
            // Point Process Analysis
            if (control as PointProcessAnalysisControl != null)
                return ((PointProcessAnalysisControl)control).Element;
            // Mixture Analysis
            if (control as MixtureAnalysisControl != null)
                return ((MixtureAnalysisControl)control).Element;
            // Composite Analysis
            if (control as CompositeAnalysisControl != null)
                return ((CompositeAnalysisControl)control).Element;
            // Bivariate Analysis
            if (control as BivariateAnalysisControl != null)
                return ((BivariateAnalysisControl)control).Element;
            // Coincident Frequency Analysis
            if (control as CoincidentFrequencyControl != null)
                return ((CoincidentFrequencyControl)control).Element;
            // Rating Curve Analysis
            if (control as RatingCurveAnalysisControl != null)
                return ((RatingCurveAnalysisControl)control).Element;
            // Time Series Analysis
            if (control as TimeSeriesAnalysisControl != null)
                return ((TimeSeriesAnalysisControl)control).Element;

            // Check if properties control
            // Time Series
            if (control as TimeSeriesPropertiesControl != null)
                return ((TimeSeriesPropertiesControl)control).Element;
            // Input Data
            if (control as InputDataPropertiesControl != null)
                return ((InputDataPropertiesControl)control).Element;
            // Fitting Analysis
            if (control as FittingAnalysisPropertiesControl != null)
                return ((FittingAnalysisPropertiesControl)control).Element;
            // Univariate Analysis
            if (control as UnivariateAnalysisPropertiesControl != null)
                return ((UnivariateAnalysisPropertiesControl)control).Element;
            // Bulletin 17C Analysis
            if (control as B17CAnalysisPropertiesControl != null)
                return ((B17CAnalysisPropertiesControl)control).Element;
            // Point Process Analysis
            if (control as PointProcessAnalysisPropertiesControl != null)
                return ((PointProcessAnalysisPropertiesControl)control).Element;
            // Mixture Analysis
            if (control as MixtureAnalysisPropertiesControl != null)
                return ((MixtureAnalysisPropertiesControl)control).Element;
            // Composite Analysis
            if (control as CompositeAnalysisPropertiesControl != null)
                return ((CompositeAnalysisPropertiesControl)control).Element;
            // Bivariate Analysis
            if (control as BivariateAnalysisPropertiesControl != null)
                return ((BivariateAnalysisPropertiesControl)control).Element;
            // Coincident Frequency Analysis
            if (control as CoincidentFrequencyPropertiesControl != null)
                return ((CoincidentFrequencyPropertiesControl)control).Element;
            // Rating Curve Analysis
            if (control as RatingCurveAnalysisPropertiesControl != null)
                return ((RatingCurveAnalysisPropertiesControl)control).Element;
            // Time Series Analysis
            if (control as TimeSeriesAnalysisPropertiesControl != null)
                return ((TimeSeriesAnalysisPropertiesControl)control).Element;

            return null;
        }

        /// <summary>
        /// Handles cleanup operations when a document control is closed. Saves plot settings to the element
        /// and unregisters event handlers to prevent memory leaks.
        /// </summary>
        /// <param name="documentControl">The document control that is being closed.</param>
        /// <remarks>
        /// This method performs essential cleanup operations for each control type:
        /// <list type="bullet">
        /// <item>Serializes and saves all plot configurations back to the element</item>
        /// <item>Unsubscribes from plot properties events</item>
        /// <item>Unsubscribes from preview control clicked events</item>
        /// </list>
        /// Proper cleanup ensures that user customizations to plots are preserved and that
        /// no memory leaks occur from orphaned event handlers.
        /// </remarks>
        public override void DocumentClosed(UIElement documentControl)
        {
            // Time Series
            if (documentControl as TimeSeriesControl != null)
            {
                var cntrl = (TimeSeriesControl)documentControl;
                cntrl.PlotPropertiesCalled -= PlotPropertiesCalled;
                cntrl.PreviewControlClicked -= DocumentControl_PreviewClicked;
            }

            // Input Data
            if (documentControl as InputDataControl != null)
            {
                var cntrl = (InputDataControl)documentControl;
                cntrl.PlotPropertiesCalled -= PlotPropertiesCalled;
                cntrl.PreviewControlClicked -= DocumentControl_PreviewClicked;
            }

            // Fitting Analysis
            if (documentControl as FittingAnalysisControl != null)
            {
                var cntrl = (FittingAnalysisControl)documentControl;
                cntrl.PlotPropertiesCalled -= PlotPropertiesCalled;
                cntrl.PreviewControlClicked -= DocumentControl_PreviewClicked;
            }

            // Univariate Analysis
            if (documentControl as UnivariateAnalysisControl != null)
            {
                var cntrl = (UnivariateAnalysisControl)documentControl;
                cntrl.PlotPropertiesCalled -= PlotPropertiesCalled;
                cntrl.PreviewControlClicked -= DocumentControl_PreviewClicked;
            }

            // Bulletin 17C Analysis — element owns plots, no serialization needed on close
            if (documentControl as B17CAnalysisControl != null)
            {
                var cntrl = (B17CAnalysisControl)documentControl;
                cntrl.PlotPropertiesCalled -= PlotPropertiesCalled;
                cntrl.PreviewControlClicked -= DocumentControl_PreviewClicked;
            }

            // Point Process Analysis — plots are owned by Element, no serialization needed on close
            if (documentControl as PointProcessAnalysisControl != null)
            {
                var cntrl = (PointProcessAnalysisControl)documentControl;
                cntrl.PlotPropertiesCalled -= PlotPropertiesCalled;
                cntrl.PreviewControlClicked -= DocumentControl_PreviewClicked;
            }

            // Mixture Analysis — plots are owned by Element, no serialization needed on close
            if (documentControl as MixtureAnalysisControl != null)
            {
                var cntrl = (MixtureAnalysisControl)documentControl;
                cntrl.PlotPropertiesCalled -= PlotPropertiesCalled;
                cntrl.PreviewControlClicked -= DocumentControl_PreviewClicked;
            }

            // Composite Analysis — plots are owned by Element, no serialization needed on close
            if (documentControl as CompositeAnalysisControl != null)
            {
                var cntrl = (CompositeAnalysisControl)documentControl;
                cntrl.PlotPropertiesCalled -= PlotPropertiesCalled;
                cntrl.PreviewControlClicked -= DocumentControl_PreviewClicked;
            }

            // Bivariate Analysis — plots are owned by Element, no serialization needed on close
            if (documentControl as BivariateAnalysisControl != null)
            {
                var cntrl = (BivariateAnalysisControl)documentControl;
                cntrl.PlotPropertiesCalled -= PlotPropertiesCalled;
                cntrl.PreviewControlClicked -= DocumentControl_PreviewClicked;
            }

            // Coincident Frequency Analysis — plots are owned by Element, no serialization needed on close
            if (documentControl as CoincidentFrequencyControl != null)
            {
                var cntrl = (CoincidentFrequencyControl)documentControl;
                cntrl.PlotPropertiesCalled -= PlotPropertiesCalled;
                cntrl.PreviewControlClicked -= DocumentControl_PreviewClicked;
            }

            // Rating Curve Analysis — plots are owned by Element, no serialization needed on close
            if (documentControl as RatingCurveAnalysisControl != null)
            {
                var cntrl = (RatingCurveAnalysisControl)documentControl;
                cntrl.PlotPropertiesCalled -= PlotPropertiesCalled;
                cntrl.PreviewControlClicked -= DocumentControl_PreviewClicked;
            }

            // Time Series Analysis — plots are owned by Element, no serialization needed on close
            if (documentControl as TimeSeriesAnalysisControl != null)
            {
                var cntrl = (TimeSeriesAnalysisControl)documentControl;
                cntrl.PlotPropertiesCalled -= PlotPropertiesCalled;
                cntrl.PreviewControlClicked -= DocumentControl_PreviewClicked;
                cntrl.Element = null;
            }

            return;
        }

        /// <summary>
        /// Handles cleanup operations when a properties control is closed. Clears element references
        /// to prevent memory leaks and ensure proper garbage collection.
        /// </summary>
        /// <param name="documentControl">The properties control that is being closed.</param>
        /// <remarks>
        /// This method sets the Element property of each properties control to null, breaking the
        /// reference to the underlying data element and allowing proper cleanup. This is important
        /// for memory management in long-running sessions with many document operations.
        /// </remarks>
        public override void PropertiesClosed(UIElement documentControl)
        {
            // Time Series
            if (documentControl as TimeSeriesPropertiesControl != null)
                ((TimeSeriesPropertiesControl)documentControl).Element = null;

            // Input Data
            if (documentControl as InputDataPropertiesControl != null)
                ((InputDataPropertiesControl)documentControl).Element = null;

            // Fitting Analysis
            if (documentControl as FittingAnalysisPropertiesControl != null)
                ((FittingAnalysisPropertiesControl)documentControl).Element = null;

            // Univariate Analysis
            if (documentControl as UnivariateAnalysisPropertiesControl != null)
                ((UnivariateAnalysisPropertiesControl)documentControl).Element = null;

            // Bulletin 17C Analysis
            if (documentControl as B17CAnalysisPropertiesControl != null)
                ((B17CAnalysisPropertiesControl)documentControl).Element = null;

            // Point Process Analysis
            if (documentControl as PointProcessAnalysisPropertiesControl != null)
                ((PointProcessAnalysisPropertiesControl)documentControl).Element = null;

            // Mixture Analysis
            if (documentControl as MixtureAnalysisPropertiesControl != null)
                ((MixtureAnalysisPropertiesControl)documentControl).Element = null;

            // Composite Analysis
            if (documentControl as CompositeAnalysisPropertiesControl != null)
                ((CompositeAnalysisPropertiesControl)documentControl).Element = null;

            // Bivariate Analysis
            if (documentControl as BivariateAnalysisPropertiesControl != null)
                ((BivariateAnalysisPropertiesControl)documentControl).Element = null;

            // Coincident Frequency Analysis
            if (documentControl as CoincidentFrequencyPropertiesControl != null)
                ((CoincidentFrequencyPropertiesControl)documentControl).Element = null;

            // Rating Curve Analysis
            if (documentControl as RatingCurveAnalysisPropertiesControl != null)
                ((RatingCurveAnalysisPropertiesControl)documentControl).Element = null;

            // Time Series Analysis
            if (documentControl as TimeSeriesAnalysisPropertiesControl != null)
                ((TimeSeriesAnalysisPropertiesControl)documentControl).Element = null;
        }

        /// <summary>
        /// Retrieves a properties control for the specified element. This method is currently not implemented
        /// as RMC-BestFit uses document-based properties controls rather than element-based ones.
        /// </summary>
        /// <param name="element">The element to create a properties control for.</param>
        /// <returns>Always returns null as this method is not implemented.</returns>
        /// <remarks>
        /// Properties controls in RMC-BestFit are created based on the active document control rather than
        /// directly from elements. See <see cref="GetPropertiesControl(UIElement)"/> for the implemented approach.
        /// </remarks>
        public override Control GetPropertiesControl(IElement element)
        {
            return null;
        }

        /// <summary>
        /// Creates and returns the appropriate properties control for the specified document control.
        /// Handles special logic for plot properties controls to prevent unnecessary closure and recreation.
        /// </summary>
        /// <param name="documentControl">The document control to create a properties panel for.</param>
        /// <returns>
        /// A <see cref="Control"/> instance configured as the properties panel for the document control,
        /// or null if the plot properties control is already open for the current plot or if the control
        /// type is not recognized.
        /// </returns>
        /// <remarks>
        /// This method implements smart properties panel management:
        /// <list type="bullet">
        /// <item>Detects if the plot properties control is already open for the current plot and avoids reopening</item>
        /// <item>Creates element-specific properties controls for each analysis and data type</item>
        /// <item>Handles the _plotPropertiesOpen and _plotPropertiesClosing flags to coordinate state</item>
        /// </list>
        /// The method checks all plot types within each control to determine if the properties panel
        /// should remain open or be replaced with a new properties control.
        /// </remarks>
        public override Control GetPropertiesControl(UIElement documentControl)
        {
            bool isOpen = _plotPropertiesOpen;
            _plotPropertiesOpen = false;
            // The plot properties check has to be done in each if statement below because we have to do an equality check on the plot.
            // Time Series
            if (documentControl as TimeSeriesControl != null)
            {
                if (isOpen == true && _plotPropertiesClosing == false)
                {
                    var plot = ((TimeSeriesControl)documentControl).GetCurrentPlot();
                    if (plot != null && _plotPropertiesControl.Plot.Equals(plot))
                    {
                        _plotPropertiesOpen = true;
                        return null;
                    }
                }
                var element = ((TimeSeriesControl)documentControl).Element;
                return new TimeSeriesPropertiesControl() { Element = element };
            }

            // Input Data
            if (documentControl as InputDataControl != null)
            {
                if (isOpen == true && _plotPropertiesClosing == false)
                {
                    var plot = ((InputDataControl)documentControl).GetCurrentPlot();
                    if (plot != null && _plotPropertiesControl.Plot.Equals(plot))
                    {
                        _plotPropertiesOpen = true;
                        return null;
                    }
                }
                var element = ((InputDataControl)documentControl).Element;
                return new InputDataPropertiesControl() { Element = element };
            }

            // Fitting Analysis
            if (documentControl as FittingAnalysisControl != null)
            {
                if (isOpen == true && _plotPropertiesClosing == false)
                {
                    var plot = ((FittingAnalysisControl)documentControl).GetCurrentPlot();
                    if (plot != null && _plotPropertiesControl.Plot.Equals(plot))
                    {
                        _plotPropertiesOpen = true;
                        return null;
                    }
                }
                var element = ((FittingAnalysisControl)documentControl).Element;
                return new FittingAnalysisPropertiesControl() { Element = element };
            }

            // Univariate Analysis
            if (documentControl as UnivariateAnalysisControl != null)
            {
                if (isOpen == true && _plotPropertiesClosing == false)
                {
                    var plot = ((UnivariateAnalysisControl)documentControl).GetCurrentPlot();
                    if (plot != null && _plotPropertiesControl.Plot.Equals(plot))
                    {
                        _plotPropertiesOpen = true;
                        return null;
                    }
                }
                var element = ((UnivariateAnalysisControl)documentControl).Element;
                return new UnivariateAnalysisPropertiesControl() { Element = element };
            }

            // Bulletin 17C Analysis
            if (documentControl as B17CAnalysisControl != null)
            {
                if (isOpen == true && _plotPropertiesClosing == false)
                {
                    var plot = ((B17CAnalysisControl)documentControl).GetCurrentPlot();
                    if (plot != null && _plotPropertiesControl.Plot.Equals(plot))
                    {
                        _plotPropertiesOpen = true;
                        return null;
                    }
                }
                var element = ((B17CAnalysisControl)documentControl).Element;
                return new B17CAnalysisPropertiesControl() { Element = element };
            }

            // Point Process Analysis
            if (documentControl as PointProcessAnalysisControl != null)
            {
                if (isOpen == true && _plotPropertiesClosing == false)
                {
                    var plot = ((PointProcessAnalysisControl)documentControl).GetCurrentPlot();
                    if (plot != null && _plotPropertiesControl.Plot.Equals(plot))
                    {
                        _plotPropertiesOpen = true;
                        return null;
                    }
                }
                var element = ((PointProcessAnalysisControl)documentControl).Element;
                return new PointProcessAnalysisPropertiesControl() { Element = element };
            }

            // Mixture Analysis
            if (documentControl as MixtureAnalysisControl != null)
            {
                if (isOpen == true && _plotPropertiesClosing == false)
                {
                    var plot = ((MixtureAnalysisControl)documentControl).GetCurrentPlot();
                    if (plot != null && _plotPropertiesControl.Plot.Equals(plot))
                    {
                        _plotPropertiesOpen = true;
                        return null;
                    }
                }
                var element = ((MixtureAnalysisControl)documentControl).Element;
                return new MixtureAnalysisPropertiesControl() { Element = element };
            }

            // Composite Analysis
            if (documentControl as CompositeAnalysisControl != null)
            {
                if (isOpen == true && _plotPropertiesClosing == false)
                {
                    var plot = ((CompositeAnalysisControl)documentControl).Element?.FrequencyPlot;
                    if (plot != null && _plotPropertiesControl.Plot != null && _plotPropertiesControl.Plot.Equals(plot))
                    {
                        _plotPropertiesOpen = true;
                        return null;
                    }
                }
                var element = ((CompositeAnalysisControl)documentControl).Element;
                return new CompositeAnalysisPropertiesControl() { Element = element };
            }

            // Bivariate Analysis
            if (documentControl as BivariateAnalysisControl != null)
            {
                if (isOpen == true && _plotPropertiesClosing == false)
                {
                    var plot = ((BivariateAnalysisControl)documentControl).GetCurrentPlot();
                    if (plot != null && _plotPropertiesControl.Plot.Equals(plot))
                    {
                        _plotPropertiesOpen = true;
                        return null;
                    }
                }
                var element = ((BivariateAnalysisControl)documentControl).Element;
                return new BivariateAnalysisPropertiesControl() { Element = element };
            }

            // Coincident Frequency Analysis
            if (documentControl as CoincidentFrequencyControl != null)
            {
                if (isOpen == true && _plotPropertiesClosing == false)
                {
                    var plot = ((CoincidentFrequencyControl)documentControl).GetCurrentPlot();
                    if (plot != null && _plotPropertiesControl.Plot.Equals(plot))
                    {
                        _plotPropertiesOpen = true;
                        return null;
                    }
                }
                var element = ((CoincidentFrequencyControl)documentControl).Element;
                return new CoincidentFrequencyPropertiesControl() { Element = element };
            }

            // Rating Curve Analysis
            if (documentControl as RatingCurveAnalysisControl != null)
            {
                if (isOpen == true && _plotPropertiesClosing == false)
                {
                    var plot = ((RatingCurveAnalysisControl)documentControl).GetCurrentPlot();
                    if (plot != null && _plotPropertiesControl.Plot.Equals(plot))
                    {
                        _plotPropertiesOpen = true;
                        return null;
                    }
                }
                var element = ((RatingCurveAnalysisControl)documentControl).Element;
                return new RatingCurveAnalysisPropertiesControl() { Element = element };
            }

            // Time Series Analysis
            if (documentControl as TimeSeriesAnalysisControl != null)
            {
                if (isOpen == true && _plotPropertiesClosing == false)
                {
                    var plot = ((TimeSeriesAnalysisControl)documentControl).GetCurrentPlot();
                    if (plot != null && _plotPropertiesControl.Plot.Equals(plot))
                    {
                        _plotPropertiesOpen = true;
                        return null;
                    }
                }
                var element = ((TimeSeriesAnalysisControl)documentControl).Element;
                return new TimeSeriesAnalysisPropertiesControl() { Element = element };
            }

            return null;
        }

        #endregion

        #region OxyPlot

        /// <summary>
        /// Handles requests to show or toggle the OxyPlot properties control panel. Manages the visibility state
        /// of the properties panel and coordinates which plot's properties are currently being displayed.
        /// </summary>
        /// <param name="plotRequestingProperties">The OxyPlot plot instance that is requesting the properties panel.</param>
        /// <param name="openProperties">Indicates whether to open the properties control if it is not already open.</param>
        /// <param name="propertyExpander">The specific property expander section to open within the properties control.</param>
        /// <param name="selectedObject">The OxyPlot object (axis, series, annotation, etc.) that has been selected by the user for editing.</param>
        /// <remarks>
        /// This method implements toggle behavior: if the properties panel is already open for the requesting plot
        /// and no specific object is selected, the panel will be closed. Otherwise, the panel is opened or updated
        /// to show the requested plot's properties. The method also handles expanding specific property sections
        /// when a plot element is selected.
        /// </remarks>
        private void PlotPropertiesCalled(Plot plotRequestingProperties, bool openProperties, OxyPlotPropertiesControl.PropertyEXP? propertyExpander, object selectedObject)
        {
            // Defensive: if _plotPropertiesOpen was set true but _plotPropertiesControl.Plot
            // was reset to null by a separate code path, dereferencing .Plot.Equals would NRE.
            if (_plotPropertiesOpen && _plotPropertiesControl.Plot != null && _plotPropertiesControl.Plot.Equals(plotRequestingProperties) && selectedObject == null)
            {
                ClosePlotProperties_Click(_plotPropertiesControl.Plot);
                _plotPropertiesOpen = false;
            }
            else if (openProperties == true)
            {
                _plotPropertiesControl.Plot = plotRequestingProperties;
                RaiseSetPropertiesControl(_plotPropertiesControl);
                _plotPropertiesOpen = true;
            }

            if (_plotPropertiesOpen)
            {
                _plotPropertiesControl.ExpandProperty(
                    propertyExpander ?? OxyPlotPropertiesControl.PropertyEXP.General_PlotTitle,
                    selectedObject);
            }
        }

        /// <summary>
        /// Closes the OxyPlot properties control panel if it is currently open for the specified plot.
        /// Sets appropriate state flags to coordinate the closure operation.
        /// </summary>
        /// <param name="plotRequestingProperties">The OxyPlot plot instance that is requesting closure of the properties panel.</param>
        /// <remarks>
        /// This method verifies that the properties panel is open and that it is displaying properties for
        /// the requesting plot before initiating closure. The _plotPropertiesClosing flag is set during the
        /// closure operation to prevent recursive calls or interference with other state management operations.
        /// If the properties panel is not open or is showing a different plot, the method returns without action.
        /// </remarks>
        private void ClosePlotProperties_Click(Plot plotRequestingProperties)
        {
            if (_plotPropertiesControl.Plot == null || _plotPropertiesOpen == false) return;
            if (_plotPropertiesControl.Plot.Equals(plotRequestingProperties) == false) return;
            _plotPropertiesClosing = true;
            RaiseClosePropertiesControl(_plotPropertiesControl);
            _plotPropertiesOpen = false;
            _plotPropertiesClosing = false;
        }

        /// <summary>
        /// Handles preview click events on document controls to determine if the plot properties panel should be closed.
        /// Closes the properties panel if the user clicked outside the plot and toolbar areas.
        /// </summary>
        /// <param name="plotClicked">Indicates whether the plot area itself was clicked by the user.</param>
        /// <param name="toolbarClicked">Indicates whether the plot's toolbar was clicked by the user.</param>
        /// <param name="plot">The plot instance associated with the clicked document control.</param>
        /// <remarks>
        /// This method implements user-friendly behavior where clicking on the document control's background
        /// or other non-plot areas will automatically close the plot properties panel. This helps maintain
        /// a clean interface and clear user intent. If either the plot or toolbar was clicked, the properties
        /// panel remains open to allow continued editing.
        /// </remarks>
        private void DocumentControl_PreviewClicked(bool plotClicked, bool toolbarClicked, Plot plot)
        {
            if (plotClicked == false && toolbarClicked == false)
                ClosePlotProperties_Click(plot);          
        }

        #endregion

    }
}
