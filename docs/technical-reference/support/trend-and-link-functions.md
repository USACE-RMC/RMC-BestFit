# Trend and Link Functions

[<- Previous: Spatial Extremes](../spatial/spatial-extremes.md) | [Back to Index](../../index.md) | [Next: References ->](../../references.md)

Trend and link functions support nonstationary models by mapping covariates into parameter space while respecting parameter constraints.

## Trend Function API

| API | Purpose |
|-----|---------|
| `ITrendModel` | Common nonstationary parameter function interface |
| `TrendModelBase` | Shared implementation for trend functions |
| `TrendModelType` | Enum used for serialization and UI selection |
| `ConstantTrend` | Constant parameter |
| `LinearTrend` | Linear covariate effect |
| `QuadraticTrend` | Quadratic covariate effect |
| `CubicTrend` | Cubic covariate effect |
| `ExponentialTrend` | Exponential covariate effect |
| `LogisticTrend` | Logistic transition |
| `PowerTrend` | Power-law covariate effect |
| `ReciprocalTrend` | Reciprocal covariate effect |
| `SinusoidalTrend` | Seasonal/cyclic behavior |
| `StepFunction` | Piecewise step behavior |
| `GeneralLinearFunction` | Multi-covariate linear predictor |

## Link Function API

| API | Purpose |
|-----|---------|
| `SESLink` | Supports skewed-error-space style parameter transformations |
| `ASinHLink` | Applies inverse-hyperbolic-sine style transformations |
| `CenteredLink` | Centers a parameter before transforming |
| `LogASinHLink` | Combines log and inverse-hyperbolic-sine transformations |
| `LogSESLink` | Log-scale skewed-error-space transformation |
| `BestFitLinkFunctionFactory` | Restores link functions from XML |
| Model parameter transform settings | Keep scale, probability, and bounded parameters valid |

## Usage Pattern

```cs
using RMC.BestFit.Models.TrendFunctions;

var trend = new LinearTrend();
trend.Parameters[0].Value = 1.0;
trend.Parameters[1].Value = 0.25;

double value = trend.Predict(index: 10);

Console.WriteLine(value);
```

## Spatial Regression

`GeneralLinearFunction` is used by spatial GEV models for covariate regression on location, scale, or shape parameters. Link functions keep transformed parameters in numerically valid ranges.

## Documentation Rule

When adding a nonstationary model page, document both the trend-space equation and the inverse transformation used to return to natural parameter space.

## Implementation Sources

Primary source paths: `src/RMC.BestFit/Models/TrendFunctions`, `src/RMC.BestFit/Models/LinkFunctions`, `src/RMC.BestFit/Models/UnivariateDistribution/UnivariateDistribution.cs`, and `src/RMC.BestFit/Models/SpatialExtremes/SpatialGEV.cs`.

---

[<- Previous: Spatial Extremes](../spatial/spatial-extremes.md) | [Back to Index](../../index.md) | [Next: References ->](../../references.md)
