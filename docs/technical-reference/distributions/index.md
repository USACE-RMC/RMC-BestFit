# Distribution API

[<- Previous: Input Data Frame](../data-frame/index.md) | [Back to Index](../../index.md) | [Next: Univariate Distributions ->](univariate.md)

BestFit distribution models wrap Numerics probability distributions with hydrologic likelihoods, priors, censoring support, posterior propagation, and analysis workflows.

## Coverage Map

| Page | Public API Covered |
|------|--------------------|
| [Univariate Distributions](univariate.md) | `UnivariateDistribution`, `UnivariateDistributionType`, quantile priors |
| [Mixture Models](mixture.md) | `MixtureModel`, `MixtureAnalysis` |
| [Competing Risks](competing-risks.md) | `CompetingRisksModel`, `CompetingRiskAnalysis` |
| [Point Process Models](point-process.md) | `PointProcessModel`, `PointProcessAnalysis` |
| [Composite Distributions](composite.md) | `CompositeAnalysis`, `CompositeType`, `AverageMethod`, `WeightedUnivariateAnalysis` |

## Supported Univariate Distribution Types

| Distribution | Enum Value | Typical Use |
|--------------|------------|-------------|
| Exponential | `Exponential` | Waiting times, threshold excess simplification |
| Gamma | `GammaDistribution` | Positive skewed variables |
| Generalized Extreme Value | `GeneralizedExtremeValue` | Annual maxima |
| Generalized Logistic | `GeneralizedLogistic` | Flexible hydrologic frequency analysis |
| Generalized Normal | `GeneralizedNormal` | Flexible symmetric and skewed data |
| Generalized Pareto | `GeneralizedPareto` | Peaks-over-threshold |
| Gumbel | `Gumbel` | Light-tailed annual maxima |
| Kappa Four | `KappaFour` | Flexible four-parameter frequency model |
| Ln-Normal | `LnNormal` | Natural-log transformed normal model |
| Logistic | `Logistic` | Symmetric distribution with heavier tails than Normal |
| Log-Normal | `LogNormal` | Positive multiplicative processes |
| Log-Pearson Type III | `LogPearsonTypeIII` | Bulletin 17C-style flood frequency |
| Normal | `Normal` | Symmetric or transformed data |
| Pearson Type III | `PearsonTypeIII` | Skewed data |
| Weibull | `Weibull` | Positive data and minima/extremes |

## Common Construction Pattern

```cs
using Numerics.Distributions;
using RMC.BestFit;
using RMC.BestFit.Models;

var dataFrame = new DataFrame
{
    ExactSeries = new ExactSeries(new[] { 10.2, 12.7, 15.9, 18.4 })
};

var model = new UnivariateDistribution(
    dataFrame,
    UnivariateDistributionType.GeneralizedExtremeValue);
```

## Organization Rule

Use single-distribution pages for a fitted physical process. Use composite pages when multiple processes, populations, candidate models, or uncertainty in model choice are part of the analysis.

## Implementation Sources

Primary source paths: `src/RMC.BestFit/Models/UnivariateDistribution`, `src/RMC.BestFit/Analyses/Univariate`, and `src/RMC.BestFit/Models/BivariateDistribution`.

---

[<- Previous: Input Data Frame](../data-frame/index.md) | [Back to Index](../../index.md) | [Next: Univariate Distributions ->](univariate.md)
