# Mixture Models

[<- Previous: Univariate Distributions](univariate.md) | [Back to Index](../../index.md) | [Next: Competing Risks ->](competing-risks.md)

`MixtureModel` represents a weighted mixture of flood-generating populations. It is appropriate when observations may arise from different physical populations, such as rainfall floods and snowmelt floods.

## Mathematical Form

For component distributions `F_k` with weights `w_k`,

```math
F(x) = \sum_{k=1}^{K} w_k F_k(x), \quad \sum_{k=1}^{K} w_k = 1
```

The corresponding density is

```math
f(x) = \sum_{k=1}^{K} w_k f_k(x)
```

BestFit evaluates this through `Numerics.Distributions.Mixture`. The model parameter vector contains component distribution parameters plus mixture weights; `SetParameterValues(...)` pushes those values into the Numerics mixture before likelihood evaluation.

For exact data, the contribution is the log density of the mixture at the observed value. Uncertain, interval, and threshold observations follow the same BestFit data-type semantics as `UnivariateDistribution`, but the density and CDF calls are made on the configured mixture distribution. `PointwiseDataLogLikelihood(...)` preserves that same decomposition for WAIC, LOO-CV, and diagnostics.

## Public API

| API | Purpose |
|-----|---------|
| `MixtureModel` | Model-layer mixture distribution |
| `MixtureAnalysis` | Bayesian workflow for the mixture model |
| `Parameters` | Component parameters plus mixture weights |
| `DataLogLikelihood(...)` | Mixture likelihood for BestFit data types |
| `GenerateRandomValues(...)` | Simulates from the weighted mixture |
| `SetParameterValues(...)` | Updates component parameters and weights |

## Usage Pattern

```cs
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;

var model = new MixtureModel();
var analysis = new MixtureAnalysis(model);

analysis.BayesianAnalysis.Iterations = 5000;
analysis.BayesianAnalysis.WarmupIterations = 2500;

var validation = analysis.Validate();
if (validation.IsValid)
{
    await analysis.RunAsync();
}
```

## Interpretation

Mixture weights represent the probability that a future event belongs to each latent population. They do not represent the probability of simultaneous processes; use [Competing Risks](competing-risks.md) for annual maxima formed from multiple concurrent processes.

## Implementation Notes

The current implementation supports one to three component distributions. Parameter priors are evaluated component-by-component. When Jeffreys scale priors are enabled, BestFit applies the scale-parameter penalty for each component distribution that exposes a scale parameter. Quantile priors are evaluated on the mixture quantile rather than on individual component quantiles.

## Implementation Sources

Primary source paths: `src/RMC.BestFit/Models/UnivariateDistribution/MixtureModel.cs`, `src/RMC.BestFit/Analyses/Univariate/MixtureAnalysis.cs`, and `src/RMC.BestFit/Models/UnivariateDistribution/UnivariateDistributionModelBase.cs`.

## References

<a id="1">[1]</a> G. McLachlan and D. Peel, *Finite Mixture Models*. New York, NY, USA: Wiley, 2000.

---

[<- Previous: Univariate Distributions](univariate.md) | [Back to Index](../../index.md) | [Next: Competing Risks ->](competing-risks.md)
