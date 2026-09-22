using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using FrameworkInterfaces;
using OxyPlot;
using OxyPlot.Wpf;
using RMC.BestFit.UI;

namespace PlotReferenceExporter;

internal static class Program
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var options = Parse(args);
            Export(options);
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.ToString());
            return 1;
        }
    }

    private static Dictionary<string, string> Parse(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i += 2)
        {
            if (i + 1 >= args.Length || !args[i].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("Expected --project, --element, --plot-id, --variant and --output values.");
            options[args[i][2..]] = args[i + 1];
        }
        foreach (string name in new[] { "project", "element", "plot-id", "variant", "output" })
            if (!options.ContainsKey(name)) throw new ArgumentException($"Missing --{name}.");
        return options;
    }

    private static void Export(Dictionary<string, string> options)
    {
        string sourcePath = Path.GetFullPath(options["project"]);
        string output = Path.GetFullPath(options["output"]);
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Saved source project not found", sourcePath);
        string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath))).ToLowerInvariant();
        string repo = FindRepository(AppContext.BaseDirectory);
        string mapPath = Path.Combine(repo, "skills", "bestfit-frequency", "references", "app-plot-map.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(mapPath));
        JsonElement slot = manifest.RootElement.GetProperty("slots").EnumerateArray()
            .FirstOrDefault(item => item.GetProperty("plotId").GetString() == options["plot-id"]);
        if (slot.ValueKind == JsonValueKind.Undefined)
            throw new ArgumentException($"Unknown app plot slot {options["plot-id"]}.");
        string[] variants = slot.GetProperty("variants").EnumerateArray().Select(item => item.GetString()!).ToArray();
        if (!variants.Contains(options["variant"]))
            throw new ArgumentException($"Unsupported variant {options["variant"]} for {options["plot-id"]}.");

        string outputDir = Path.GetDirectoryName(output)!;
        Directory.CreateDirectory(outputDir);
        string copyDir = Path.Combine(outputDir, ".source-copy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(copyDir);
        string copyPath = Path.Combine(copyDir, Path.GetFileName(sourcePath));
        File.Copy(sourcePath, copyPath);
        try
        {
            InitializeWpf();
            BestFitProject project = BestFitProject.GetInstance();
            project.FullFileName = copyPath;
            project.Open();
            IElement element = FindElement(project, options["element"]);
            string population = slot.GetProperty("appPopulation").GetString()!;
            string controlName = Path.GetFileName(population.Split("::")[0]).Split('.')[0];
            string updateMethod = population.Split("::")[1];
            string factory = slot.GetProperty("appFactory").GetString()!.Split("::")[1];
            string propertyName = factory["CreateDefault".Length..];
            bool sharedDiagnostic = options["plot-id"].StartsWith("shared_diagnostics.", StringComparison.Ordinal);
            object plotOwner = sharedDiagnostic
                ? element.GetType().GetProperty("BayesianPlots")?.GetValue(element)
                    ?? throw new NotSupportedException($"Element {element.Name} has no saved Bayesian plot controller.")
                : element;
            Plot plot = ResetFactoryPlot(plotOwner, factory, propertyName);
            Type controlType = typeof(RMC_BestFit.InputDataControl).Assembly.GetType("RMC_BestFit." + controlName)
                ?? throw new NotSupportedException($"App control {controlName} was not found.");
            object control = Activator.CreateInstance(controlType)!;
            if (sharedDiagnostic)
            {
                controlType.GetMethod("SetPlot")?.Invoke(control, new object[] { plot });
                if (options["variant"].StartsWith("gmm_", StringComparison.Ordinal))
                {
                    object gmm = element.GetType().GetProperty("GMM")?.GetValue(element)
                        ?? throw new NotSupportedException($"Element {element.Name} has no saved GMM analysis.");
                    PropertyInfo mode = controlType.GetProperty("Mode")!;
                    mode.SetValue(control, Enum.Parse(mode.PropertyType, "GMM"));
                    controlType.GetProperty("GMMAnalysis")!.SetValue(control, gmm);
                }
                else
                {
                    PropertyInfo analysisProperty = controlType.GetProperty("Analysis")
                        ?? throw new NotSupportedException($"{controlName} has no Analysis property.");
                    object analysis = element.GetType().GetProperty("BayesianAnalysis")?.GetValue(element)
                        ?? throw new NotSupportedException($"Element {element.Name} has no saved Bayesian analysis.");
                    analysisProperty.SetValue(control, analysis);
                }
            }
            else
            {
                PropertyInfo elementProperty = controlType.GetProperty("Element")
                    ?? throw new NotSupportedException($"{controlName} has no Element property.");
                if (!elementProperty.PropertyType.IsInstanceOfType(element))
                    throw new ArgumentException($"Element {element.Name} is not valid for {options["plot-id"]}.");
                elementProperty.SetValue(control, element);
            }
            string variantSelection = sharedDiagnostic ? "pending control initialization"
                : ConfigureVariant(control, element, options["plot-id"], options["variant"], variants);
            if (options["plot-id"].StartsWith("fitting.", StringComparison.Ordinal))
            {
                MethodInfo loaded = controlType.GetMethod("UserControl_Loaded", InstanceMembers)
                    ?? throw new NotSupportedException("Fitting control has no Loaded initialization method.");
                loaded.Invoke(control, new object?[] { control, new RoutedEventArgs() });
            }
            if (sharedDiagnostic)
            {
                MethodInfo loaded = controlType.GetMethod("UserControl_Loaded", InstanceMembers)
                    ?? throw new NotSupportedException($"{controlName} has no Loaded initialization method.");
                loaded.Invoke(control, new object?[] { control, new RoutedEventArgs() });
                variantSelection = ConfigureVariant(control, element, options["plot-id"], options["variant"], variants);
            }
            if (options["plot-id"].StartsWith("rating.residual", StringComparison.Ordinal)
                || options["plot-id"].StartsWith("time_series_analysis.residual", StringComparison.Ordinal))
            {
                controlType.GetMethod("GetResiduals", InstanceMembers)?.Invoke(control, null);
            }
            if (options["plot-id"].StartsWith("input_data.mean_", StringComparison.Ordinal)
                || options["plot-id"] is "input_data.modified_scale" or "input_data.shape")
            {
                controlType.GetMethod("UpdateControl", InstanceMembers)?.Invoke(control, null);
                controlType.GetMethod("UpdateThresholdDiagnosticsPlots", InstanceMembers)?.Invoke(control, null);
            }
            MethodInfo method = controlType.GetMethod(updateMethod, InstanceMembers)
                ?? throw new NotSupportedException($"{controlName}.{updateMethod} was not found.");
            object?[] arguments = method.GetParameters().Select(parameter => parameter.IsOptional
                ? Type.Missing
                : throw new NotSupportedException($"{controlName}.{updateMethod} needs an explicit {parameter.Name} argument.")).ToArray();
            method.Invoke(control, arguments);

            plot.Measure(new Size(1000, 650));
            plot.Arrange(new Rect(0, 0, 1000, 650));
            plot.UpdateLayout();
            plot.InvalidatePlot(true);
            PlotModel model = plot.ActualModel as PlotModel
                ?? throw new InvalidOperationException("App plot population did not create an OxyPlot model.");
            ((IPlotModel)model).Update(true);
            using (var svg = File.Create(output + ".svg"))
                new OxyPlot.SvgExporter { Width = 1000, Height = 650, IsDocument = true }.Export(model, svg);
            using (var png = File.Create(output + ".png"))
                new OxyPlot.Wpf.PngExporter { Width = 1000, Height = 650, Resolution = 96 }.Export(model, png);

            var axes = model.Axes.Select(axis => new
            {
                type = axis.GetType().Name,
                axis.Key,
                axis.Title,
                position = axis.Position.ToString(),
                Minimum = Finite(axis.Minimum),
                Maximum = Finite(axis.Maximum),
                ActualMinimum = Finite(axis.ActualMinimum),
                ActualMaximum = Finite(axis.ActualMaximum),
                labels = axis.GetType().GetProperty("Labels", InstanceMembers)?.GetValue(axis) is IEnumerable labels
                    ? labels.Cast<object>().Select(label => label?.ToString()).ToArray() : null,
            }).ToList();
            var series = model.Series.Select((item, index) => new
            {
                name = index < plot.Series.Count ? plot.Series[index].Name : item.Title,
                type = item.GetType().Name,
                item.Title,
                item.IsVisible,
                item.RenderInLegend,
                points = SnapshotPoints(item),
                points2 = SnapshotPoints(item, "ActualPoints2"),
                items = SnapshotPoints(item, "Items"),
                actualItems = SnapshotPoints(item, "ActualItems"),
                grid = SnapshotGrid(item),
                contours = SnapshotContours(item),
                contourLevels = item.GetType().GetProperty("ContourLevels", InstanceMembers)?.GetValue(item) as double[],
                style = SnapshotScalars(item, "Color", "Fill", "FillColor", "MarkerFill", "MarkerStroke", "MarkerType", "MarkerSize", "LineStyle", "StrokeThickness", "StrokeColor", "ErrorBarColor"),
            }).ToList();
            var annotations = model.Annotations.Select(item => new
            {
                type = item.GetType().Name,
                text = item.GetType().GetProperty("Text", InstanceMembers)?.GetValue(item) as string,
                geometry = SnapshotScalars(item, "Type", "X", "Y", "MinimumX", "MaximumX", "MinimumY", "MaximumY", "Slope", "Intercept", "StartPoint", "EndPoint"),
                style = SnapshotScalars(item, "Color", "Fill", "Stroke", "LineStyle", "StrokeThickness"),
            }).ToList();
            string? requiredSeries = (options["plot-id"], options["variant"]) switch
            {
                ("input_data.frequency", "exact") => "ExactData",
                ("input_data.frequency", "interval") => "IntervalData",
                ("input_data.frequency", "uncertain") => "UncertainData",
                ("input_data.frequency", "low_outlier") => "LowOutlierData",
                ("b17c.frequency", "historical_interval") => "IntervalData",
                ("b17c.frequency", "low_outlier") => "LowOutlierData",
                ("point_process.frequency", "ams") => "AMSExactData",
                ("mixture.frequency", "component") => "series_0",
                ("composite.frequency", "component") => "series_0",
                ("time_series_analysis.series", "forecast") => "PredictionCredibleIntervals",
                ("univariate.frequency", "quantile_prior") => "QuantilePrior",
                _ => null,
            };
            if (requiredSeries != null && !series.Any(item => item.name == requiredSeries
                && (item.points.Count > 0 || item.points2.Count > 0 || item.actualItems.Count > 0)))
                throw new NotSupportedException($"Requested variant lacks populated app series {requiredSeries} in {element.Name}.");
            if (options["plot-id"].StartsWith("fitting.", StringComparison.Ordinal)
                && options["variant"] == "comparison" && series.Count(item => item.points.Count > 0) < 2)
                throw new NotSupportedException("Saved fitting selection has fewer than two plotted distributions.");
            if ((options["plot-id"] is "univariate.frequency" or "coincident.frequency")
                && options["variant"] == "comparison"
                && !series.Any(item => item.name?.StartsWith("PosteriorPredictive_", StringComparison.Ordinal) == true
                    && item.points.Count > 0))
                throw new NotSupportedException("Requested comparison lacks a populated app alternative curve.");
            var geometry = new
            {
                formatVersion = 1,
                sourceSha256 = hash,
                runtime = new
                {
                    framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                    exporterSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Program).Assembly.Location))).ToLowerInvariant(),
                    appSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(RMC_BestFit.InputDataControl).Assembly.Location))).ToLowerInvariant(),
                },
                project = sourcePath,
                element = element.Name,
                plotId = options["plot-id"],
                variant = options["variant"],
                variantSelection,
                appFactory = slot.GetProperty("appFactory").GetString(),
                factoryReset = true,
                appPopulation = population,
                title = plot.Title,
                axes,
                series,
                annotations,
            };
            File.WriteAllText(output + ".json", JsonSerializer.Serialize(geometry, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(output);
        }
        finally
        {
            Directory.Delete(copyDir, recursive: true);
        }
    }

    private static void InitializeWpf()
    {
        if (Application.Current == null) _ = new Application();
        FrameworkUI.ThemeManager.SetTheme(FrameworkUI.ThemeColor.Light);
        Application.Current!.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/GenericControls;component/Themes/GenericControlsTheme.xaml", UriKind.Absolute)
        });
        Application.Current!.Resources["Center_CellStyle"] = new Style(typeof(DataGridCell));
        Application.Current.Resources["Left_CellStyle"] = new Style(typeof(DataGridCell));
        Application.Current.Resources["DataGridEditingTextBoxStyle"] = new Style(typeof(TextBox));
    }

    private static IElement FindElement(BestFitProject project, string name)
    {
        var collections = project.ElementCollections
            ?? throw new InvalidOperationException("Project has no element collections.");
        foreach (IElementCollection collection in collections)
            for (int index = 0; index < collection.Count; index++)
            {
                IElement? candidate = collection[index];
                if (candidate?.Name == name) return candidate;
            }
        throw new ArgumentException($"No element named {name} in saved project.");
    }

    private static List<Dictionary<string, object?>> SnapshotPoints(object series, string propertyName = "ActualPoints")
    {
        PropertyInfo? property = series.GetType().GetProperty(propertyName, InstanceMembers);
        if (property?.GetValue(series) is not IEnumerable points)
            return new List<Dictionary<string, object?>>();
        var output = new List<Dictionary<string, object?>>();
        foreach (object? point in points)
        {
            if (point == null) continue;
            var values = new Dictionary<string, object?>();
            foreach (string key in new[] { "X", "Y", "Value", "Size", "RangeStart", "RangeEnd", "Area", "Count", "CategoryIndex", "Error", "LowerErrorX", "UpperErrorX", "LowerErrorY", "UpperErrorY" })
            {
                object? value = point.GetType().GetProperty(key, InstanceMembers)?.GetValue(point);
                if (value is double number) values[key] = Finite(number);
                else if (value is float single) values[key] = float.IsFinite(single) ? single : null;
                else if (value is decimal or int or long) values[key] = value;
            }
            output.Add(values);
        }
        return output;
    }

    private static double? Finite(double value) => double.IsFinite(value) ? value : null;

    private static Plot ResetFactoryPlot(object owner, string factoryName, string propertyName)
    {
        const BindingFlags factoryFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        MethodInfo? method = null;
        for (Type? type = owner.GetType(); type != null && method == null; type = type.BaseType)
            method = type.GetMethod(factoryName, factoryFlags | BindingFlags.DeclaredOnly);
        if (method == null)
            throw new NotSupportedException($"{owner.GetType().Name}.{factoryName} factory was not found.");
        if (method.GetParameters().Length != 0)
            throw new NotSupportedException($"{factoryName} needs arguments; cannot reset without app inputs.");
        Plot plot = method.Invoke(method.IsStatic ? null : owner, null) as Plot
            ?? throw new InvalidOperationException($"{factoryName} did not create an app plot.");
        string backingName = "_" + propertyName;
        FieldInfo? backing = null;
        for (Type? type = owner.GetType(); type != null && backing == null; type = type.BaseType)
            backing = type.GetFields(InstanceMembers | BindingFlags.DeclaredOnly)
                .FirstOrDefault(field => string.Equals(field.Name, backingName, StringComparison.OrdinalIgnoreCase)
                    && typeof(Plot).IsAssignableFrom(field.FieldType));
        if (backing == null)
            throw new NotSupportedException($"{owner.GetType().Name} has no backing field for {propertyName}.");
        backing.SetValue(owner, plot);
        if (!ReferenceEquals(owner.GetType().GetProperty(propertyName)?.GetValue(owner), plot))
            throw new InvalidOperationException($"{propertyName} did not expose its factory-reset plot.");
        return plot;
    }

    private static Dictionary<string, object?> SnapshotScalars(object source, params string[] names)
    {
        var values = new Dictionary<string, object?>();
        foreach (string name in names)
        {
            object? value = source.GetType().GetProperty(name, InstanceMembers)?.GetValue(source);
            if (value is double number) values[name] = Finite(number);
            else if (value is float single) values[name] = float.IsFinite(single) ? single : null;
            else if (value is decimal or int or long or bool) values[name] = value;
            else if (value is Enum || value is OxyColor || value is DataPoint) values[name] = value.ToString();
        }
        return values;
    }

    private static object? SnapshotGrid(object series)
    {
        if (series.GetType().GetProperty("Data", InstanceMembers)?.GetValue(series) is not double[,] data)
            return null;
        double?[][] matrix = Enumerable.Range(0, data.GetLength(0))
            .Select(row => Enumerable.Range(0, data.GetLength(1))
                .Select(column => Finite(data[row, column])).ToArray()).ToArray();
        object? Read(string name) => series.GetType().GetProperty(name, InstanceMembers)?.GetValue(series);
        return new
        {
            data = matrix,
            columnCoordinates = Read("ColumnCoordinates") as double[],
            rowCoordinates = Read("RowCoordinates") as double[],
            x0 = Read("X0") is double x0 ? Finite(x0) : null,
            x1 = Read("X1") is double x1 ? Finite(x1) : null,
            y0 = Read("Y0") is double y0 ? Finite(y0) : null,
            y1 = Read("Y1") is double y1 ? Finite(y1) : null,
        };
    }

    private static object? SnapshotContours(object series)
    {
        if (series.GetType().GetField("contours", InstanceMembers)?.GetValue(series) is not IEnumerable contours)
            return null;
        var output = new List<object>();
        foreach (object contour in contours)
        {
            object? level = contour.GetType().GetField("ContourLevel", InstanceMembers)?.GetValue(contour);
            object? points = contour.GetType().GetField("Points", InstanceMembers)?.GetValue(contour);
            if (points is not IEnumerable path) continue;
            var coordinates = new List<Dictionary<string, object?>>();
            foreach (object point in path)
            {
                var x = point.GetType().GetProperty("X")?.GetValue(point);
                var y = point.GetType().GetProperty("Y")?.GetValue(point);
                coordinates.Add(new Dictionary<string, object?>
                {
                    ["X"] = x is double xv ? Finite(xv) : null,
                    ["Y"] = y is double yv ? Finite(yv) : null,
                });
            }
            output.Add(new { level, points = coordinates });
        }
        return output;
    }

    private static string ConfigureVariant(object control, IElement element, string plotId, string variant, string[] variants)
    {
        if (plotId == "input_data.qq")
        {
            string radio = variant == "real" ? "RealSpaceRadioButton" : "LogSpaceRadioButton";
            ControlMember<System.Windows.Controls.Primitives.ToggleButton>(control, radio).IsChecked = true;
            return $"{radio}=true";
        }
        if (plotId == "input_data.chronology")
        {
            string expected = variant == "water_year" ? "Water Year" : "Calendar Year";
            if (!element.Name.EndsWith(expected, StringComparison.Ordinal))
                throw new ArgumentException($"Chronology variant {variant} requires a saved {expected} element.");
            return $"saved {expected} input data";
        }
        if (plotId == "input_data.frequency" && variant != "default")
            return $"saved {variant} observation data; required series checked after app population";
        if (plotId.StartsWith("shared_diagnostics.", StringComparison.Ordinal))
        {
            if (plotId == "shared_diagnostics.trace" && variant == "warmup")
            {
                ControlMember<CheckBox>(control, "IncludeWarmUpCheckBox").IsChecked = true;
                return "IncludeWarmUpCheckBox=true";
            }
            if (plotId == "shared_diagnostics.histogram" && variant == "prior")
            {
                ControlMember<CheckBox>(control, "ShowPriorDistribution").IsChecked = true;
                return "ShowPriorDistribution=true";
            }
            if (plotId == "shared_diagnostics.influence")
            {
                int index = variant switch
                {
                    "bayesian_fit" or "gmm_fit" => 1,
                    "bayesian_variance" or "gmm_variance" => 2,
                    "leave_one_out" => 3,
                    _ => 0,
                };
                ControlMember<ComboBox>(control, "ViewComboBox").SelectedIndex = index;
                return $"Mode={(variant.StartsWith("gmm_") ? "GMM" : "Bayesian")};ViewComboBox={index}";
            }
            return "saved Bayesian analysis and control defaults";
        }
        if (plotId == "bivariate.distribution")
        {
            string[] parts = variant.Split('_');
            int plotIndex = parts[0] switch
            {
                "scatter" => 0,
                "density" => 1,
                "joint" => 2,
                _ => throw new ArgumentException($"Unknown bivariate variant {variant}.")
            };
            int axisIndex = parts[^1] == "cdf" ? 1 : 0;
            ControlMember<ComboBox>(control, "PlotTypeComboBox").SelectedIndex = plotIndex;
            ControlMember<ComboBox>(control, "AxisTypeComboBox").SelectedIndex = axisIndex;
            control.GetType().GetMethod("BindAxisTitles", InstanceMembers)?.Invoke(control, null);
            return $"PlotTypeComboBox={plotIndex};AxisTypeComboBox={axisIndex}";
        }
        if (plotId.StartsWith("fitting.", StringComparison.Ordinal) && variant == "comparison")
            return "saved fitting control with multiple selected distributions";
        if (plotId is ("univariate.frequency" or "coincident.frequency") && variant == "comparison")
            return "AlternativeSelector checked " + SelectSavedAlternative(control, element);
        if (plotId == "univariate.frequency" && variant == "quantile_prior"
            && element.Name.Contains("Quantile Priors", StringComparison.Ordinal))
            return "saved enabled quantile prior source";
        if (plotId == "univariate.frequency" && variant == "nonstationary"
            && element.Name.StartsWith("NSFFA", StringComparison.Ordinal))
            return "saved nonstationary analysis";
        if (plotId == "univariate.chronology" && variant == "nonstationary"
            && element.Name.StartsWith("NSFFA", StringComparison.Ordinal))
            return "saved nonstationary analysis";
        if (plotId == "b17c.frequency" && variant != "mv_normal")
            return $"saved B17C {variant} source element";
        if (plotId == "point_process.frequency")
        {
            ControlMember<CheckBox>(control, "POTCheckbox").IsChecked = variant != "ams";
            ControlMember<CheckBox>(control, "SeasonalCheckbox").IsChecked = variant == "seasonal";
            if (variant == "comparison")
                return "POTCheckbox=true;SeasonalCheckbox=false;AlternativeSelector checked "
                    + SelectSavedAlternative(control, element);
            return $"POTCheckbox={variant != "ams"};SeasonalCheckbox={variant == "seasonal"}";
        }
        if (plotId == "mixture.frequency" && variant is ("zero_inflated" or "component"))
            return $"saved mixture {variant} element; required series checked after app population";
        if (plotId == "composite.frequency" && variant == "component")
            return "saved competing-risk component curves";
        if (plotId == "time_series_analysis.series" && variant == "forecast")
            return "saved time-series analysis forecast result";
        if (plotId == "rating.curve" && variant == "segmented"
            && element.Name.Contains("Segment Rating Curve", StringComparison.Ordinal))
            return "saved multi-segment rating curve";
        if (plotId == "coincident.frequency" && variant == "linear_response")
            return "default app frequency y-axis is linear";
        if (variant == "default" || variant == variants[0]) return "app control defaults and saved element data";
        throw new NotSupportedException($"Variant {variant} needs an explicit app control or source-data selection.");
    }

    private static T ControlMember<T>(object control, string name) where T : class
        => (control.GetType().GetField(name, InstanceMembers)?.GetValue(control)
            ?? control.GetType().GetProperty(name, InstanceMembers)?.GetValue(control)) as T
            ?? throw new NotSupportedException($"App control {control.GetType().Name} has no {name}.");

    private static string SelectSavedAlternative(object control, IElement element)
    {
        object selector = ControlMember<object>(control, "AlternativeSelector");
        selector.GetType().GetProperty("Element")!.SetValue(selector, element);
        if (selector.GetType().GetProperty("AnalysisList")?.GetValue(selector) is not IEnumerable alternatives)
            throw new NotSupportedException("App alternative selector has no saved analysis list.");
        foreach (object item in alternatives)
        {
            object? candidate = item.GetType().GetProperty("Alternative")?.GetValue(item);
            if (candidate == null || candidate.GetType() != element.GetType()) continue;
            if (candidate.GetType().GetProperty("AnalysisResults")?.GetValue(candidate) == null) continue;
            item.GetType().GetProperty("IsChecked")!.SetValue(item, true);
            return candidate.GetType().GetProperty("Name")?.GetValue(candidate) as string ?? "unnamed analysis";
        }
        throw new NotSupportedException($"No fitted sibling analysis found for {element.Name}.");
    }

    private static string FindRepository(string current)
    {
        for (DirectoryInfo? dir = new DirectoryInfo(current); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "skills", "bestfit-frequency", "references", "app-plot-map.json")))
                return dir.FullName;
        throw new FileNotFoundException("Cannot locate app-plot-map.json above the exporter executable.");
    }
}
