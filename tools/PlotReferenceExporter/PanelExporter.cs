using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FrameworkInterfaces;
using FrameworkUI.ProjectExplorer;
using RMC.BestFit.UI;

namespace PlotReferenceExporter;

/// <summary>Captures real desktop panels from disposable saved-project copies.</summary>
internal static class PanelExporter
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    internal static void Export(Dictionary<string, string> options)
    {
        string source = Path.GetFullPath(options["project"]);
        string output = Path.GetFullPath(options["output"]);
        string panel = options["panel"];
        if (panel is not ("properties" or "explorer" or "dss-selector"))
            throw new ArgumentException($"Unknown panel {panel}.");
        if (!File.Exists(source)) throw new FileNotFoundException("Saved project not found", source);
        string before = Hash(source);
        string? dssSource = options.TryGetValue("dss-file", out string? file) ? Path.GetFullPath(file) : null;
        string? dssBefore = dssSource == null ? null : Hash(dssSource);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        string copyDir = Path.Combine(Path.GetDirectoryName(output)!, ".panel-source-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(copyDir);
        string projectCopy = Path.Combine(copyDir, Path.GetFileName(source));
        File.Copy(source, projectCopy);
        try
        {
            InitializeWpf();
            BestFitProject project = BestFitProject.GetInstance();
            project.FullFileName = projectCopy;
            project.Open();
            Type? requestedType = options.TryGetValue("control-type", out string? typeName)
                ? typeof(RMC_BestFit.InputDataControl).Assembly.GetType(typeName.Contains('.') ? typeName : "RMC_BestFit." + typeName) : null;
            if (typeName != null && requestedType == null)
                throw new ArgumentException($"Desktop control type {typeName} was not found.");
            Type? elementType = requestedType?.GetProperty("Element")?.PropertyType;
            IElement element = FindElement(project, options["element"], elementType);
            var controller = new RMC_BestFit.MainProjectNode(project);
            FrameworkElement visual;
            object nativeControl;
            var configuration = new Dictionary<string, object?>();
            if (panel == "properties")
            {
                var document = controller.GetDocumentControl(element)
                    ?? throw new InvalidOperationException("No native desktop document for selected element.");
                var control = controller.GetPropertiesControl((UIElement)document)
                    ?? throw new InvalidOperationException("No desktop Properties control for the selected element.");
                if (requestedType != null && !requestedType.IsInstanceOfType(control))
                    throw new ArgumentException($"Properties control {control.GetType().FullName} does not match {requestedType.FullName}.");
                visual = control;
                nativeControl = control;
            }
            else if (panel == "explorer")
            {
                controller.Load();
                var tree = new ProjectExplorerTreeView { ProjectNode = controller };
                controller.IsExpanded = true;
                bool found = ConfigureTree(controller, element);
                if (!found) throw new InvalidOperationException("Selected source element is absent from the native project tree.");
                visual = tree;
                nativeControl = tree;
                configuration["expandedFolders"] = controller.ChildNodes.Cast<object>()
                    .Select(n => n.GetType().GetProperty("NodeHeader", Members)?.GetValue(n))
                    .Select(n => n?.GetType().GetProperty("HeaderText", Members)?.GetValue(n)?.ToString()).ToArray();
            }
            else
            {
                if (dssSource == null || !options.TryGetValue("dss-path", out string? wanted))
                    throw new ArgumentException("DSS selector requires --dss-file and --dss-path.");
                // HEC-DSS may update its catalog/index while reading; isolate every native open.
                string dssCopy = Path.Combine(copyDir, Path.GetFileName(dssSource));
                File.Copy(dssSource, dssCopy);
                var selector = new RMC_BestFit.DSSPathSelectorWindow { FullFileName = dssCopy };
                var grid = (DataGrid)selector.FindName("MyDataGrid");
                object[] matches = grid.Items.Cast<object>().Where(row =>
                    string.Equals(row.GetType().GetProperty("DatelessPath", Members)?.GetValue(row)?.ToString(), wanted, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (matches.Length != 1)
                    throw new ArgumentException($"Expected one DSS record matching {wanted}; found {matches.Length}.");
                grid.SelectedItem = matches[0];
                grid.ScrollIntoView(grid.SelectedItem);
                string selected = grid.SelectedItem.GetType().GetProperty("DatelessPath", Members)?.GetValue(grid.SelectedItem)?.ToString() ?? "";
                if (!string.Equals(selected, wanted, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("DSS native selector did not retain requested selection.");
                configuration["dssRequestedPath"] = wanted;
                configuration["dssSelectedPath"] = selected;
                configuration["dssDisplayedPath"] = ((TextBox)selector.FindName("PathNameTextBox")).Text;
                configuration["dssReadPath"] = dssCopy;
                configuration["catalogRows"] = grid.Items.Count;
                configuration["dialogTitle"] = selector.Title;
                // Preserve the native dialog's content, not a recreated representation or title bar.
                visual = selector.Content as FrameworkElement
                    ?? throw new InvalidOperationException("DSS selector has no native content visual.");
                selector.Content = null;
                nativeControl = selector;
            }

            int width = ReadSize(options, "width", panel == "dss-selector" ? 1050 : panel == "explorer" ? 620 : 650);
            visual.Measure(new Size(width, double.PositiveInfinity));
            int naturalHeight = Math.Clamp((int)Math.Ceiling(visual.DesiredSize.Height) + 8, 300, 1400);
            int height = ReadSize(options, "height", panel == "dss-selector" ? 620 : naturalHeight);
            var surface = new Border { Background = Application.Current.FindResource("EnvironmentWindowBackground") as Brush ?? Brushes.White, Child = visual };
            using (var host = new HwndSource(new HwndSourceParameters("BestFit documentation panel export")
            {
                Width = width, Height = height, PositionX = -32000, PositionY = -32000,
                WindowStyle = unchecked((int)0x80000000), // hidden WS_POPUP; no visible interactive window
            }))
            {
                host.RootVisual = surface;
                surface.Measure(new Size(width, height));
                surface.Arrange(new Rect(0, 0, width, height));
                surface.UpdateLayout();
                Pump();
                surface.UpdateLayout();
                if (panel == "explorer") ConfigureTree(controller, element);
                Pump();
                surface.UpdateLayout();
                var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(surface);
                var png = new PngBitmapEncoder();
                png.Frames.Add(BitmapFrame.Create(bitmap));
                using (var stream = File.Create(output + ".png")) png.Save(stream);
                configuration["visibleControls"] = SnapshotControls(surface);
                host.RootVisual = null;
            }
            string after = Hash(source);
            string? dssAfter = dssSource == null ? null : Hash(dssSource);
            if (after != before || dssAfter != dssBefore)
                throw new InvalidOperationException("Original project or DSS hash changed during panel capture.");
            var provenance = new
            {
                formatVersion = 1, source, project = source, element = element.Name, sourceSha256 = before, sourceSha256After = after,
                runtime = new
                {
                    framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                    exporterSha256 = Hash(typeof(PanelExporter).Assembly.Location),
                    appSha256 = Hash(typeof(RMC_BestFit.InputDataControl).Assembly.Location),
                },
                dssSource, dssSourceSha256 = dssBefore, dssSourceSha256After = dssAfter,
                selectedElement = element.Name, selectedElementType = element.GetType().FullName,
                panel, nativeControlType = nativeControl.GetType().FullName,
                captureMethod = "Actual WPF control rendered through hidden HwndSource to RenderTargetBitmap",
                capturedRegion = panel == "dss-selector" ? "native dialog content; operating-system title bar excluded" : "native panel control",
                theme = "Light", width, height, dpi = 96,
                sourceSettings = SnapshotSettings(element), configuration,
                pngSha256 = Hash(output + ".png"), generatedAtUtc = DateTime.UtcNow.ToString("O"),
                savedAnalysisRerun = false, originalProjectWritten = false,
            };
            File.WriteAllText(output + ".json", JsonSerializer.Serialize(provenance, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Captured {panel}: {element.Name} -> {output}.png");
        }
        finally
        {
            // Only remove the exact disposable directory created under this output directory.
            string resolved = Path.GetFullPath(copyDir);
            string parent = Path.GetFullPath(Path.GetDirectoryName(output)!) + Path.DirectorySeparatorChar;
            if (resolved.StartsWith(parent, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(resolved).StartsWith(".panel-source-", StringComparison.Ordinal))
                Directory.Delete(resolved, true);
            if (Hash(source) != before || (dssSource != null && Hash(dssSource) != dssBefore))
                throw new InvalidOperationException("Original source hash changed, including during failed capture.");
        }
    }

    private static int ReadSize(Dictionary<string, string> options, string key, int fallback)
    {
        int value = options.TryGetValue(key, out string? input) ? int.Parse(input, CultureInfo.InvariantCulture) : fallback;
        return value is >= 200 and <= 2400 ? value : throw new ArgumentOutOfRangeException(key);
    }

    private static void InitializeWpf()
    {
        if (Application.Current == null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Application app = Application.Current ?? throw new InvalidOperationException("WPF application initialization failed.");
        FrameworkUI.ThemeManager.SetTheme(FrameworkUI.ThemeColor.Light);
        foreach (string uri in new[]
        {
            "pack://application:,,,/FrameworkUI;component/Icons/IconDictionary.xaml",
            "pack://application:,,,/GenericControls;component/Themes/GenericControlsTheme.xaml",
            "pack://application:,,,/OxyPlotControls;component/Themes/OxyPlotControlsTheme.xaml",
            "pack://application:,,,/NumericControls;component/Themes/NumericControlsTheme.xaml",
            "pack://application:,,,/DatabaseControls;component/Themes/DatabaseControlsTheme.xaml",
            "pack://application:,,,/RMC.BestFit.UI;component/Resources/IconDictionary.xaml",
            "pack://application:,,,/RMC-BestFit;component/Resources/IconDictionary.xaml",
            "pack://application:,,,/GenericControls;component/Resources/ResourceDictionary.xaml",
        }) app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(uri, UriKind.Absolute) });
        app.Resources["Center_CellStyle"] = new Style(typeof(DataGridCell));
        app.Resources["Left_CellStyle"] = new Style(typeof(DataGridCell));
        app.Resources["DataGridEditingTextBoxStyle"] = new Style(typeof(TextBox));
    }

    private static IElement FindElement(BestFitProject project, string name, Type? required)
    {
        var matches = new List<IElement>();
        foreach (IElementCollection collection in project.ElementCollections ?? throw new InvalidOperationException("Project has no element collections."))
            for (int i = 0; i < collection.Count; i++)
                if (collection[i]?.Name == name && (required == null || required.IsInstanceOfType(collection[i]))) matches.Add(collection[i]);
        return matches.Count == 1 ? matches[0] : throw new ArgumentException($"Expected unique selected element {name}; found {matches.Count}.");
    }

    private static bool ConfigureTree(Node node, IElement element)
    {
        bool selected = ReferenceEquals(node.GetType().GetProperty("Element", Members)?.GetValue(node), element);
        bool descendant = false;
        foreach (Node child in node.ChildNodes) descendant |= ConfigureTree(child, element);
        node.IsExpanded = node is RMC_BestFit.MainProjectNode || node is ElementNodeCollection || descendant;
        if (selected)
        {
            node.IsNodeSelected = true;
            node.IsSelected = true;
        }
        return selected || descendant;
    }

    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static Dictionary<string, object?> SnapshotSettings(IElement element)
    {
        var result = new Dictionary<string, object?>();
        foreach (string key in new[] { "Name", "UnitLabel", "IndexLabel", "EntryMethod", "SeriesType", "USGSSiteNumber", "GHCNSiteNumber", "CHMNSiteNumber", "ABOMSiteNumber", "DepthUnit", "TimeInterval", "StartDateTime", "HECDSSFullFilename", "HECDSSDataPathname", "ExactDataMethod", "BlockFunction", "TimeBlock", "POTThresholdValue", "MinStepsBetweenPeaks", "SmoothingFunction", "Period", "UseMultipleGrubbsBeckTest" })
        {
            object? value = element.GetType().GetProperty(key, Members)?.GetValue(element);
            if (value != null) result[key] = Convert.ToString(value, CultureInfo.InvariantCulture);
        }
        return result;
    }

    private static List<Dictionary<string, object?>> SnapshotControls(DependencyObject parent)
    {
        var result = new List<Dictionary<string, object?>>();
        void Visit(DependencyObject item)
        {
            if (item is FrameworkElement view && view.IsVisible)
            {
                string? text = item switch
                {
                    TextBox input => input.Text,
                    TextBlock label => label.Text,
                    ComboBox combo => combo.SelectedItem?.GetType().GetProperty("DisplayName")?.GetValue(combo.SelectedItem)?.ToString() ?? combo.Text,
                    CheckBox check => check.Content?.ToString(),
                    Button button => button.Content as string,
                    _ => null,
                };
                if (!string.IsNullOrWhiteSpace(text)) result.Add(new() { ["type"] = item.GetType().Name, ["name"] = view.Name, ["text"] = text, ["enabled"] = view.IsEnabled });
            }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(item); i++) Visit(VisualTreeHelper.GetChild(item, i));
        }
        Visit(parent);
        return result;
    }
}
