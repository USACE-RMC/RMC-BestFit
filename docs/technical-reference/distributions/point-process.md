# Point Process Models

[<- Previous: Competing Risks](competing-risks.md) | [Back to Index](../../index.md) | [Next: Composite Distributions ->](composite.md)

`PointProcessModel` supports peaks-over-threshold frequency analysis. Instead of modeling annual maxima, it models threshold exceedances and their occurrence rate.

## Mathematical Form

BestFit implements the nonhomogeneous extreme-value point-process likelihood using GEV-compatible location, scale, and shape parameters, not a standalone generalized-Pareto excess model. Let `u` be the threshold, `Ny` the number of observation years, and let the fitted GEV parameters be location $\mu$, scale $\sigma>0$, and Coles shape $\xi$. For an exceedance $x_i > u$:

```math
z_i = 1 + \xi\frac{x_i-\mu}{\sigma}, \qquad z_i > 0
```

For $\xi \ne 0$, BestFit uses:

```math
\log L =
\sum_i \left[-\log \sigma - \left(1+\frac{1}{\xi}\right)\log z_i\right]
- N_y \left(1+\xi\frac{u-\mu}{\sigma}\right)^{-1/\xi}
```

For $|\xi| < 10^{-4}$, the implementation switches to the Gumbel limit:

```math
\log L =
\sum_i \left[-\log\sigma-\frac{x_i-\mu}{\sigma}\right]
- N_y\exp\left[-\frac{u-\mu}{\sigma}\right].
```

Numerics stores the GEV shape with Hosking's `Kappa`; BestFit converts it to the Coles sign convention internally by using $\xi=-\kappa$.

## Public API

| API | Purpose |
|-----|---------|
| `PointProcessModel` | Peaks-over-threshold model |
| `PointProcessAnalysis` | Bayesian analysis workflow |
| `DataLogLikelihood(...)` | Exceedance likelihood |
| `PointwiseDataLogLikelihood(...)` | WAIC/LOO-CV support |
| `GenerateRandomValues(...)` | Simulates threshold exceedance behavior |

## Usage Pattern

```cs
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;

var model = new PointProcessModel();
var analysis = new PointProcessAnalysis(model);

analysis.BayesianAnalysis.Iterations = 5000;
analysis.BayesianAnalysis.WarmupIterations = 2500;

if (analysis.Validate().IsValid)
{
    await analysis.RunAsync();
}
```

## Threshold Diagnostics

Before fitting a point-process model, inspect threshold stability with `ThresholdDiagnostics`, `MeanResidualLifeResult`, and `ParameterStabilityResult` from the data-frame API.

## Seasonal Option

The seasonal implementation uses two GEV components and two day-of-year change points. BestFit validates `1 <= k1 < k2 <= 366`, assigns observations outside `[k1,k2)` to season 1 and observations inside `[k1,k2)` to season 2, and scales the Poisson rate term by each season's fraction of the year.

## Implementation Sources

Primary source paths: `src/RMC.BestFit/Models/UnivariateDistribution/PointProcessModel.cs`, `src/RMC.BestFit/Analyses/Univariate/PointProcessAnalysis.cs`, and `src/RMC.BestFit/Models/DataFrame/ThresholdDiagnostics.cs`.

## References

<a id="1">[1]</a> S. Coles, *An Introduction to Statistical Modeling of Extreme Values*. London, U.K.: Springer, 2001.

---

[<- Previous: Competing Risks](competing-risks.md) | [Back to Index](../../index.md) | [Next: Composite Distributions ->](composite.md)
