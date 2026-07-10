# Analyses Overview

[<- Previous: Diagnostics](../estimation/diagnostics.md) | [Back to Index](../../index.md) | [Next: Distribution Fitting ->](distribution-fitting.md)

An **Analysis** in ***RMC-BestFit*** coordinates the estimation of a model and manages the results. While models define the mathematical relationship between parameters and data, analyses handle the workflow: configuring estimation settings, running MCMC or MLE, storing results, computing derived quantities, and providing event notifications for GUI integration.

This chapter introduces the analysis architecture, surveys the pre-built analyses available in the library, and demonstrates how to create custom analyses for specialized applications.

## Why Analyses Matter

Consider fitting a GEV distribution to flood data. You could manually:
1. Create the model
2. Configure the Bayesian sampler
3. Run MCMC
4. Extract posterior summaries
5. Compute return level estimates
6. Generate uncertainty bands

Or you could use `UnivariateAnalysis`, which handles all of this with sensible defaults:

```cs
var analysis = new UnivariateAnalysis(model);
await analysis.RunAsync();

// Results are ready
var rl100 = analysis.GetReturnLevel(100);  // 100-year return level with uncertainty
```

Analyses encapsulate best practices and provide consistent interfaces across different model types.

## The Analysis Architecture

### The `IAnalysis` Interface

All analyses implement the `IAnalysis` interface:

```cs
public interface IAnalysis
{
    /// <summary>
    /// Gets or sets whether the analysis has been estimated.
    /// </summary>
    bool IsEstimated { get; set; }

    /// <summary>
    /// Validates the analysis configuration.
    /// </summary>
    (bool IsValid, List<string> ValidationMessages) Validate();

    /// <summary>
    /// Runs the analysis asynchronously.
    /// </summary>
    Task RunAsync(SafeProgressReporter? progressReporter = null);

    /// <summary>
    /// Cancels a running analysis.
    /// </summary>
    void CancelAnalysis();

    /// <summary>
    /// Clears the analysis results.
    /// </summary>
    void ClearResults();

    /// <summary>
    /// Event raised before analysis starts (allows cancellation).
    /// </summary>
    event EventHandler<CancelEventArgs>? AnalysisStarting;

    /// <summary>
    /// Event raised when analysis completes.
    /// </summary>
    event EventHandler<AnalysisRunCompletedEventArgs>? AnalysisCompleted;
}
```

### The `IBayesianAnalysis` Interface

Analyses that support Bayesian MCMC implement additional functionality:

```cs
public interface IBayesianAnalysis : IAnalysis
{
    /// <summary>
    /// Gets the Bayesian analysis configuration and results.
    /// </summary>
    BayesianAnalysis BayesianAnalysis { get; }
}
```

### The `IProbabilityOrdinates` Interface

Many analyses produce results at specific probability levels:

```cs
public interface IProbabilityOrdinates
{
    /// <summary>
    /// Gets the collection of probability ordinates for computing return levels.
    /// </summary>
    ProbabilityOrdinates ProbabilityOrdinates { get; }
}
```

---

## Pre-Built Analyses

***RMC-BestFit*** provides analyses for all major model types:

### Univariate Distribution Analyses

| Analysis | Model | Description |
|----------|-------|-------------|
| `FittingAnalysis` | Multiple | Fits all 15 distributions, ranks by AIC/BIC |
| `UnivariateAnalysis` | `UnivariateDistribution` | Full Bayesian analysis of single distribution |
| `MixtureAnalysis` | `MixtureModel` | Two-population mixture models |
| `CompetingRiskAnalysis` | `CompetingRisksModel` | Annual max of multiple processes |
| `PointProcessAnalysis` | `PointProcessModel` | Peaks-over-threshold |
| `Bulletin17CAnalysis` | `LogPearsonTypeIII` | Bulletin 17C-compliant analysis |
| `CompositeAnalysis` | Multiple estimated univariate analyses | Competing risks, mixtures, and model averaging |

### Time Series Analyses

| Analysis | Model | Description |
|----------|-------|-------------|
| `ARAnalysis` | `AutoRegressive` | Autoregressive models |
| `MAAnalysis` | `MovingAverage` | Moving average models |
| `ARIMAAnalysis` | `ARIMA` | Integrated ARMA models |
| `ARIMAXAnalysis` | `ARIMAX` | ARIMA with covariates |

### Other Analyses

| Analysis | Model | Description |
|----------|-------|-------------|
| `RatingCurveAnalysis` | `RatingCurve` | Stage-discharge relationships |
| `BivariateAnalysis` | `BivariateDistribution` | Joint distribution of two variables |
| `CoincidentFrequencyAnalysis` | Fitted `BivariateAnalysis` + response surface | Coincident response-frequency curve |
| `SpatialGEVAnalysis` | `SpatialGEV` | Regional frequency analysis |

---

## Analysis Workflow

### Basic Workflow

```cs
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;

// 1. Create model
var df = new DataFrame();
df.ExactSeries = new ExactSeries(annualPeaks);
var model = new UnivariateDistribution(df, UnivariateDistributionType.GeneralizedExtremeValue);

// 2. Create analysis
var analysis = new UnivariateAnalysis(model);

// 3. Configure (optional - defaults are usually good)
analysis.BayesianAnalysis.Iterations = 10000;
analysis.BayesianAnalysis.WarmupIterations = 5000;

// 4. Run
await analysis.RunAsync();

// 5. Check results
if (analysis.IsEstimated)
{
    // Access posterior summaries
    var results = analysis.BayesianAnalysis.Results;
    // ... use results
}
```

### With Progress Reporting

For GUI applications, use `SafeProgressReporter`:

```cs
using RMC.BestFit.Support;

var progressReporter = new SafeProgressReporter();

// Subscribe to progress updates
progressReporter.ProgressChanged += (sender, progress) =>
{
    Console.WriteLine($"Progress: {progress:P0}");
};

await analysis.RunAsync(progressReporter);
```

### With Cancellation

```cs
// Start analysis in background
var analysisTask = analysis.RunAsync();

// Later, if user requests cancellation:
analysis.CancelAnalysis();

// Wait for task to complete (it will be cancelled)
try
{
    await analysisTask;
}
catch (OperationCanceledException)
{
    Console.WriteLine("Analysis was cancelled.");
}
```

### With Event Handlers

```cs
// Before analysis starts (can cancel)
analysis.AnalysisStarting += (sender, args) =>
{
    Console.WriteLine("Analysis starting...");
    // args.Cancel = true;  // Set to cancel before starting
};

// After analysis completes
analysis.AnalysisCompleted += (sender, args) =>
{
    if (args.Succeeded)
    {
        Console.WriteLine("Analysis completed successfully.");
    }
    else if (args.Cancelled)
    {
        Console.WriteLine("Analysis was cancelled.");
    }
    else
    {
        Console.WriteLine($"Analysis failed: {args.Error?.Message}");
    }
};

await analysis.RunAsync();
```

---

## Computing Return Levels

Most distribution analyses support computing return levels with uncertainty:

```cs
// After running analysis
if (analysis.IsEstimated)
{
    // Get MAP (Maximum A Posteriori) return level
    var mapParams = analysis.BayesianAnalysis.Results.MAP.Values;
    model.SetParameterValues(mapParams);
    double rl100_map = model.Distribution.InverseCDF(1 - 1.0/100);

    Console.WriteLine($"100-year return level (MAP): {rl100_map:F0}");

    // Get posterior distribution of return levels
    var posteriorSamples = analysis.BayesianAnalysis.Results.Output;
    var rl100_samples = new double[posteriorSamples.Count];

    for (int i = 0; i < posteriorSamples.Count; i++)
    {
        model.SetParameterValues(posteriorSamples[i].Values);
        rl100_samples[i] = model.Distribution.InverseCDF(1 - 1.0/100);
    }

    // Compute posterior summary
    Array.Sort(rl100_samples);
    double rl100_median = rl100_samples[rl100_samples.Length / 2];
    double rl100_lower = rl100_samples[(int)(0.025 * rl100_samples.Length)];
    double rl100_upper = rl100_samples[(int)(0.975 * rl100_samples.Length)];

    Console.WriteLine($"100-year return level (median): {rl100_median:F0}");
    Console.WriteLine($"95% CI: [{rl100_lower:F0}, {rl100_upper:F0}]");
}
```

---

## XML Serialization

Analyses can be saved and restored:

```cs
// Save analysis configuration and results
var xElement = analysis.ToXElement();
xElement.Save("analysis_results.xml");

// Restore (requires the original model/data)
var loadedXml = XElement.Load("analysis_results.xml");
var restoredAnalysis = new UnivariateAnalysis(model, loadedXml);
```

---

## Creating Custom Analyses

When the pre-built analyses don't meet your needs, you can create custom analyses by inheriting from `AnalysisBase`:

```cs
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;

public class MyCustomAnalysis : AnalysisBase, IBayesianAnalysis
{
    private readonly MyCustomModel _model;
    private BayesianAnalysis _bayesianAnalysis;

    public MyCustomAnalysis(MyCustomModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _bayesianAnalysis = new BayesianAnalysis(model);
    }

    public BayesianAnalysis BayesianAnalysis => _bayesianAnalysis;

    public override (bool IsValid, List<string> ValidationMessages) Validate()
    {
        var messages = new List<string>();
        bool isValid = true;

        // Add custom validation logic
        if (_model.SomeProperty < 0)
        {
            messages.Add("SomeProperty must be non-negative.");
            isValid = false;
        }

        return (isValid, messages);
    }

    public override async Task RunAsync(SafeProgressReporter? progressReporter = null)
    {
        var validation = Validate();
        if (!validation.IsValid)
            throw new InvalidOperationException("Analysis is not valid.");

        var previewArgs = new CancelEventArgs();
        OnAnalysisStarting(previewArgs);
        if (previewArgs.Cancel)
        {
            OnAnalysisCompleted(new AnalysisRunCompletedEventArgs(true, false, null));
            return;
        }

        try
        {
            // Run Bayesian analysis
            await _bayesianAnalysis.RunAsync(progressReporter);

            IsEstimated = true;
            OnAnalysisCompleted(new AnalysisRunCompletedEventArgs(false, true, null));
        }
        catch (Exception ex)
        {
            OnAnalysisCompleted(new AnalysisRunCompletedEventArgs(false, false, ex));
            throw;
        }
    }
}
```

---

## Analysis Class Reference

### UnivariateAnalysis Properties

| Property | Type | Description |
|----------|------|-------------|
| `UnivariateDistribution` | `UnivariateDistribution` | The distribution model |
| `BayesianAnalysis` | `BayesianAnalysis` | MCMC configuration and results |
| `ProbabilityOrdinates` | `ProbabilityOrdinates` | Probability levels for output |
| `IsEstimated` | `bool` | Whether analysis has completed |

### RatingCurveAnalysis Properties

| Property | Type | Description |
|----------|------|-------------|
| `RatingCurve` | `RatingCurve` | The rating curve model |
| `BayesianAnalysis` | `BayesianAnalysis` | MCMC configuration and results |
| `NumberOfSegments` | `int` | 1, 2, or 3 segment model |

### TimeSeriesAnalysis Properties (AR, MA, ARIMA, ARIMAX)

| Property | Type | Description |
|----------|------|-------------|
| `Model` | varies | The time series model |
| `BayesianAnalysis` | `BayesianAnalysis` | MCMC configuration and results |
| `ForecastHorizon` | `int` | Number of periods to forecast |

---

## Best Practices

### Validation

Always validate before running:

```cs
var validation = analysis.Validate();
if (!validation.IsValid)
{
    foreach (var msg in validation.ValidationMessages)
    {
        Console.WriteLine($"Error: {msg}");
    }
    return;
}
```

### MCMC Settings

For routine analyses:
- `Iterations`: 10,000
- `WarmupIterations`: 5,000

For complex models (mixture, spatial):
- `Iterations`: 50,000
- `WarmupIterations`: 25,000

### Checking Convergence

After running, always check R-hat and ESS:

```cs
var results = analysis.BayesianAnalysis.Results;
bool converged = true;

for (int i = 0; i < model.Parameters.Count; i++)
{
    var stats = results.ParameterResults[i].SummaryStatistics;
    if (stats.Rhat > 1.1 || stats.ESS < 400)
    {
        Console.WriteLine($"Warning: {model.Parameters[i].Name} may not have converged.");
        converged = false;
    }
}

if (!converged)
{
    Console.WriteLine("Consider increasing iterations.");
}
```

---

## Implementation Sources

Primary source paths: `src/RMC.BestFit/Analyses/Support`, `src/RMC.BestFit/Analyses/Univariate`, `src/RMC.BestFit/Analyses/Bivariate`, `src/RMC.BestFit/Analyses/DistributionFitting`, `src/RMC.BestFit/Analyses/RatingCurve`, `src/RMC.BestFit/Analyses/TimeSeries`, and `src/RMC.BestFit/Analyses/SpatialExtremes`.

---

## References

<a id="1">[1]</a>
Gelman, A., Carlin, J.B., Stern, H.S., Dunson, D.B., Vehtari, A., and Rubin, D.B. (2013). *Bayesian Data Analysis*, Third Edition. CRC Press.

<a id="2">[2]</a>
ter Braak, C.J.F. and Vrugt, J.A. (2008). "Differential Evolution Markov Chain with snooker updater and fewer chains." *Statistics and Computing*, 18(4), 435-446.

---

[<- Previous: Diagnostics](../estimation/diagnostics.md) | [Back to Index](../../index.md) | [Next: Distribution Fitting ->](distribution-fitting.md)
