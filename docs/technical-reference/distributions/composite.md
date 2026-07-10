# Composite Distributions

[<- Previous: Point Process Models](point-process.md) | [Back to Index](../../index.md) | [Next: Model Estimation ->](../estimation/index.md)

Composite distributions combine multiple univariate analyses into a single distribution used for frequency results. BestFit exposes this through `CompositeAnalysis`, `WeightedUnivariateAnalysis`, `CompositeType`, and `AverageMethod`.

## Composite Types

| `CompositeType` | Meaning | Typical Use |
|-----------------|---------|-------------|
| `CompetingRisks` | Combines processes as a maximum or minimum | Rainfall vs. snowmelt annual maxima |
| `Mixture` | Weighted mixture of populations | Mixed flood populations |
| `ModelAverage` | Weighted average across candidate models | Model-form uncertainty |

## Model Averaging Methods

| `AverageMethod` | Weight Source |
|-----------------|---------------|
| `AIC` | Akaike Information Criterion from MLE fitting |
| `BIC` | Bayesian Information Criterion from MLE fitting |
| `DIC` | Bayesian deviance information criterion |
| `WAIC` | Watanabe-Akaike information criterion |
| `LOOIC` | PSIS leave-one-out information criterion |
| `Equal` | Equal component weights |
| `RMSE` | Plotting-position root mean square error |

For `AIC`, `BIC`, `DIC`, `WAIC`, and `LOOIC`, `CompositeAnalysis.EstimateModelWeights()` gathers one criterion value from each successfully estimated child and passes the valid criterion vector to `GoodnessOfFit.AICWeights(...)`. For `RMSE`, it calls `GoodnessOfFit.RMSEWeights(...)`. For `Equal`, it assigns `1 / Analyses.Count` to every child. Unestimated children receive zero weight and are not allowed through `RunAsync(...)`.

## Public API

| Member | Purpose |
|--------|---------|
| `CompositeAnalysis()` | Creates an empty composite |
| `CompositeAnalysis(IEnumerable<WeightedUnivariateAnalysis>)` | Creates a composite from estimated child analyses |
| `Analyses` | Weighted child analyses |
| `CompositeDistributionType` | Selects competing risks, mixture, or model averaging |
| `ModelAverageMethod` | Selects the information criterion or weighting method |
| `Dependency` | Dependency assumption from Numerics probability helpers |
| `IsMaximum` | Max/min selection for competing risks |
| `ProbabilityOrdinates` | Output frequencies |
| `BayesianAnalysis` | Presentation and posterior-propagation settings |

## Usage Pattern

```cs
using RMC.BestFit.Analyses;

var components = new[]
{
    new WeightedUnivariateAnalysis(firstAnalysis, 0.50),
    new WeightedUnivariateAnalysis(secondAnalysis, 0.50)
};

var composite = new CompositeAnalysis(components)
{
    CompositeDistributionType = CompositeType.ModelAverage,
    ModelAverageMethod = AverageMethod.WAIC
};

if (composite.Validate().IsValid)
{
    await composite.RunAsync();
}
```

## Constraints

`WeightedUnivariateAnalysis` rejects another `CompositeAnalysis` as a child. Composite-of-composite nesting is intentionally unsupported to avoid circular references and ambiguous weighting semantics.

For mixture and model-average output, BestFit constructs a `Numerics.Distributions.Mixture` from the child point-estimate or posterior-realization distributions. If the supplied weights sum to less than one, the resulting mixture is marked zero-inflated and the residual mass is stored in `ZeroWeight`. For competing risks, BestFit constructs `Numerics.Distributions.CompetingRisks`, passes through `Dependency`, and sets `MinimumOfRandomVariables = !IsMaximum`.

## Implementation Sources

Primary source paths: `src/RMC.BestFit/Analyses/Univariate/CompositeAnalysis.cs`, `src/RMC.BestFit/Analyses/Univariate/WeightedUnivariateAnalysis.cs`, and `src/RMC.BestFit/Analyses/Univariate/UncertaintyAnalysisResults.cs`.

## References

<a id="1">[1]</a> K. P. Burnham and D. R. Anderson, *Model Selection and Multimodel Inference*, 2nd ed. New York, NY, USA: Springer, 2002.

<a id="2">[2]</a> A. Vehtari, A. Gelman, and J. Gabry, "Practical Bayesian model evaluation using leave-one-out cross-validation and WAIC," *Statistics and Computing*, vol. 27, no. 5, pp. 1413-1432, 2017.

---

[<- Previous: Point Process Models](point-process.md) | [Back to Index](../../index.md) | [Next: Model Estimation ->](../estimation/index.md)
