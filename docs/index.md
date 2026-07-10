# RMC-BestFit Library Documentation

## Overview

***RMC-BestFit*** is a Bayesian-first statistical analysis framework for flood frequency studies, developed by the U.S. Army Corps of Engineers Risk Management Center. The model library supports life-safety flood risk assessments, hydrologic frequency analysis, rating curves, time series, bivariate frequency analysis, and spatial extremes.

The documentation is organized to mirror the Numerics library: a short getting-started path, ordered technical-reference chapters, page-to-page navigation, IEEE-style numeric references, and C# examples that track the public API.

## Documentation Structure

| Document | Description |
|----------|-------------|
| [Getting Started](getting-started.md) | Installation, namespaces, and first workflows |
| [Models Overview](technical-reference/models/overview.md) | `IModel`, parameters, priors, custom models |
| [Input Data Frame](technical-reference/data-frame/index.md) | Exact, uncertain, interval, and threshold data |
| [Distribution API](technical-reference/distributions/index.md) | Distribution-family map and coverage index |
| [Univariate Distributions](technical-reference/distributions/univariate.md) | All 15 supported univariate distribution models |
| [Mixture Models](technical-reference/distributions/mixture.md) | Weighted flood-population mixtures |
| [Competing Risks](technical-reference/distributions/competing-risks.md) | Maximum/minimum of multiple flood processes |
| [Point Process Models](technical-reference/distributions/point-process.md) | Peaks-over-threshold modeling |
| [Composite Distributions](technical-reference/distributions/composite.md) | Competing risks, mixtures, and model averaging |
| [Model Estimation](technical-reference/estimation/index.md) | MLE, MAP, GMM, Bayesian MCMC, information criteria |
| [Diagnostics](technical-reference/estimation/diagnostics.md) | Influence, leverage, prior influence, predictive checks |
| [Analyses Overview](technical-reference/analysis/overview.md) | Analysis workflow and shared interfaces |
| [Distribution Fitting](technical-reference/analysis/distribution-fitting.md) | Automated MLE fitting and ranking |
| [Univariate Analysis](technical-reference/analysis/univariate.md) | Bayesian frequency analysis for one distribution |
| [Composite Analysis](technical-reference/analysis/composite.md) | `CompositeAnalysis` and `WeightedUnivariateAnalysis` |
| [Bivariate Analysis](technical-reference/analysis/bivariate.md) | Copula-based joint distributions |
| [Coincident Frequency](technical-reference/analysis/coincident-frequency.md) | Response-surface frequency analysis |
| [Rating Curves](technical-reference/analysis/rating-curve.md) | Stage-discharge analysis |
| [Time Series](technical-reference/analysis/time-series.md) | AR, MA, ARIMA, and ARIMAX |
| [Spatial Extremes](technical-reference/spatial/spatial-extremes.md) | Spatial GEV and regional frequency analysis |
| [Trend and Link Functions](technical-reference/support/trend-and-link-functions.md) | Nonstationary parameter functions and link space |
| [Implementation Audit](technical-reference/implementation-audit.md) | Source-code traceability map for equations, algorithms, and examples |
| [References](references.md) | Consolidated bibliography |

## Quick Start

### Bayesian Univariate Analysis

```cs
using Numerics.Distributions;
using RMC.BestFit;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;

double[] annualPeaks =
{
    42000, 51700, 38900, 61200, 46800, 55300, 44100, 67300
};

var dataFrame = new DataFrame
{
    ExactSeries = new ExactSeries(annualPeaks)
};

var model = new UnivariateDistribution(
    dataFrame,
    UnivariateDistributionType.GeneralizedExtremeValue);

var analysis = new UnivariateAnalysis(model);
analysis.BayesianAnalysis.Iterations = 3000;
analysis.BayesianAnalysis.WarmupIterations = 1500;

await analysis.RunAsync();

var results = analysis.BayesianAnalysis.Results;
if (results is not null)
{
    for (int i = 0; i < model.Parameters.Count; i++)
    {
        var stats = results.ParameterResults[i].SummaryStatistics;
        Console.WriteLine($"{model.Parameters[i].Name}: {stats.Mean:F3}");
    }
}
```

### Fast MLE Distribution Screening

```cs
using RMC.BestFit;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;

var dataFrame = new DataFrame
{
    ExactSeries = new ExactSeries(new[] { 12.0, 15.4, 18.2, 21.0, 25.7, 30.1 })
};

var fitting = new FittingAnalysis(dataFrame);
await fitting.RunAsync();

var ranked = fitting.FittedDistributions
    .Where(candidate => candidate.FitSucceeded)
    .OrderBy(candidate => candidate.AIC);

foreach (var candidate in ranked.Take(5))
{
    Console.WriteLine($"{candidate.Distribution?.Type}: AIC = {candidate.AIC:F2}");
}
```

## API Coverage Policy

The documentation targets roughly 90% coverage of the public `RMC.BestFit.dll` API. Coverage means public types and important public members are documented in concept pages, API tables, or examples. Trivial DTO properties may be grouped, but model, estimation, analysis, diagnostic, and serialization workflows must have an explicit documented usage path.

All technical equations and algorithm descriptions are cross-checked against the production implementation listed in [Implementation Audit](technical-reference/implementation-audit.md). External references provide scientific context; source code controls the documented API behavior.

## Supplemental Public API Inventory

The following support types are covered as part of the 90% API map. They are usually consumed through the higher-level model, estimation, analysis, or diagnostic pages rather than as standalone chapters.

| API | Documentation Context |
|-----|-----------------------|
| `BatchAnalysisOptions`, `BatchAnalysisResult`, `BatchAnalysisRunner` | Batch orchestration utilities for running multiple `IAnalysis` instances |
| `BootstrapDiagnostics` | Bootstrap retry, rejection, timing, and failure-rate diagnostics |
| `Bulletin17CDistribution`, `UncertaintyMethod`, `CohnConfidenceIntervalResult` | Bulletin 17C distribution and uncertainty-support API |
| `CachedMultivariateNormal` | Spatial GEV and Gaussian-copula performance support |
| `CorrelationFunctionType`, `ICorrelationModel` | Spatial correlation model selection and common contract |
| `DataComponent`, `DataComponentType`, `PriorComponent` | Observation/prior component labeling for WAIC, LOO-CV, and diagnostics |
| `DataSeries` | Shared base type for exact, uncertain, interval, and threshold data series |
| `GMMEstimationStrategy`, `GMMIdentificationStatus`, `IGMMModel` | Generalized Method of Moments model and result-state support |
| `ISimulatable`, `IUnivariateAnalysis` | Shared simulation and univariate-analysis contracts |
| `MRLPoint`, `MeanResidualLifeResult`, `StabilityPoint`, `ParameterStabilityResult` | Threshold diagnostic outputs for point-process model selection |
| `ASinHLink`, `CenteredLink`, `LogASinHLink`, `LogSESLink`, `BestFitLinkFunctionFactory` | Link-function implementations and XML factory support |
| `ObservationLeverage`, `PriorComponentLeverage`, `PriorComponentSummary` | Diagnostics output records nested in leverage and prior-influence results |
| `ParameterPenalty`, `PriorComponentType`, `ParetoKCategory`, `PointEstimateType` | Prior, influence, and Bayesian result classification support |
| `SpatialGEVCrossValidationResults`, `SpatialGEVSiteResults`, `SpatialGEVUncertaintyMethod` | Spatial GEV site, uncertainty, and cross-validation result types |
| `SubscriptFormatter` | UI/report-friendly parameter subscript formatting helper |
| `UnivariateDistributionModelBase` | Shared base class for univariate, mixture, competing-risk, and point-process models |

## Namespaces

| Namespace | Purpose |
|-----------|---------|
| `RMC.BestFit` | Data-series and observation types |
| `RMC.BestFit.Models` | Models, data frame, parameters, trends, rating curves, time series, spatial types |
| `RMC.BestFit.Analyses` | Analysis workflows and result orchestration |
| `RMC.BestFit.Estimation` | MLE, MAP, GMM, Bayesian MCMC |
| `RMC.BestFit.Diagnostics` | Influence, leverage, and predictive diagnostics |
| `Numerics.Distributions` | Distribution implementations, priors, copulas, and distribution enums |

## Architecture

```text
Numerics.dll
    |
RMC.BestFit.dll
    |
RMC.BestFit.UI.dll
    |
RMC-BestFit.exe
```

## Key References

[1] Interagency Advisory Committee on Water Data, *Guidelines for Determining Flood Flow Frequency, Bulletin 17C*, U.S. Geological Survey, 2019.

[2] C. J. F. ter Braak and J. A. Vrugt, "Differential Evolution Markov Chain with snooker updater and fewer chains," *Statistics and Computing*, vol. 18, no. 4, pp. 435-446, 2008.

[3] A. Vehtari, A. Gelman, and J. Gabry, "Practical Bayesian model evaluation using leave-one-out cross-validation and WAIC," *Statistics and Computing*, vol. 27, no. 5, pp. 1413-1432, 2017.

## License

RMC-BestFit is released under the Zero-Clause BSD (0BSD) license. See [LICENSE](../LICENSE) for the full text.
