# Diagnostics

[<- Previous: Model Estimation](index.md) | [Back to Index](../../index.md) | [Next: Analyses Overview ->](../analysis/overview.md)

BestFit diagnostics identify observations, priors, model assumptions, and fitted results that deserve engineering review. They are exposed through `RMC.BestFit.Diagnostics` and through helper methods on estimation classes.

## Diagnostic Coverage

| API | Purpose |
|-----|---------|
| `InfluenceDiagnostics` | PSIS-LOO influence summaries and Pareto-k classification |
| `LeverageDiagnostics` | MAP leverage decomposition into fit and variance influence |
| `PriorInfluenceDiagnostics` | Prior-component contribution and prior-to-data ratios |
| `PosteriorPredictiveCheck` | Simulates replicated datasets from posterior draws |
| `PriorPredictiveCheck` | Simulates datasets from priors before fitting |
| `PredictiveCheckResults` | Common posterior predictive p-values |
| `PredictiveSummary` | Quantile summaries for generated predictive datasets |
| `MaximumLikelihood.GetObservationInfluence()` | Data-only influence at MLE |
| `MaximumAPosteriori.GetObservationInfluence()` | Posterior influence at MAP |
| `BayesianAnalysis.ComputeInfluenceDiagnostics()` | PSIS-LOO influence from MCMC results |
| `BayesianAnalysis.ComputeLeverageDiagnostics()` | Leverage at MCMC MAP |
| `BayesianAnalysis.ComputePriorInfluenceDiagnostics()` | Prior influence from posterior samples |

## PSIS-LOO Influence

`InfluenceDiagnostics` stores one `ObservationInfluence` per observation or data component. Pareto `k` values classify the reliability of the PSIS approximation.

| Category | Rule | Interpretation |
|----------|------|----------------|
| Good | `k < 0.5` | Stable approximation |
| Ok | `0.5 <= k < 0.7` | Monitor |
| Bad | `0.7 <= k < 1.0` | Influential observation |
| Very Bad | `k >= 1.0` | PSIS approximation may be unreliable |

```cs
using RMC.BestFit.Diagnostics;

InfluenceDiagnostics diagnostics = analysis.BayesianAnalysis.ComputeInfluenceDiagnostics();

foreach (var observation in diagnostics.GetProblematicObservations(0.7))
{
    Console.WriteLine($"{observation.Index}: k = {observation.ParetoK:F3}");
}
```

## Leverage Diagnostics

`LeverageDiagnostics` decomposes each observation and prior component at the MAP estimate.

```math
\text{Total Leverage} =
\text{Fit Influence} + \text{Variance Influence}
```

Fit influence is Cook's-distance-like movement in the MAP estimate. Variance influence measures how much the component contributes to posterior precision.

```cs
using RMC.BestFit.Diagnostics;

LeverageDiagnostics leverage = analysis.BayesianAnalysis.ComputeLeverageDiagnostics();

Console.WriteLine($"Total leverage: {leverage.TotalLeverage:F3}");
Console.WriteLine($"Observation leverage: {leverage.TotalObservationLeverage:F3}");
Console.WriteLine($"Prior leverage: {leverage.TotalPriorLeverage:F3}");
```

## Prior Influence

`PriorInfluenceDiagnostics` summarizes prior components by type and magnitude. It is useful when quantile priors or strong parameter priors may dominate limited data.

```cs
using RMC.BestFit.Diagnostics;

PriorInfluenceDiagnostics priors =
    analysis.BayesianAnalysis.ComputePriorInfluenceDiagnostics(thinEvery: 10);

foreach (var component in priors.GetMostConstrainingComponents(topN: 5))
{
    Console.WriteLine($"{component.Name}: {component.MeanLogLikelihood:F3}");
}
```

## Predictive Checks

Posterior predictive checks simulate new data from posterior draws; prior predictive checks simulate from priors before fitting.

```cs
using RMC.BestFit.Diagnostics;

var posteriorCheck = new PosteriorPredictiveCheck(model, analysis.BayesianAnalysis.Results!);
PredictiveCheckResults pValues = posteriorCheck.ComputeCommonPValues(numberOfReplicates: 100);

if (pValues.HasPotentialMisfit())
{
    Console.WriteLine("At least one predictive p-value is near the tail.");
}
```

## Serialization

`InfluenceDiagnostics`, `LeverageDiagnostics`, and `PriorInfluenceDiagnostics` support XML serialization through `ToXElement()` constructors or methods. Persist diagnostics with the analysis result when review reproducibility matters.

## Implementation Sources

Primary source paths: `src/RMC.BestFit/Diagnostics/InfluenceDiagnostics.cs`, `src/RMC.BestFit/Diagnostics/LeverageDiagnostics.cs`, `src/RMC.BestFit/Diagnostics/PriorInfluenceDiagnostics.cs`, `src/RMC.BestFit/Diagnostics/PosteriorPredictiveCheck.cs`, `src/RMC.BestFit/Diagnostics/PriorPredictiveCheck.cs`, and `src/RMC.BestFit/Estimation/BayesianAnalysis.cs`.

## References

<a id="1">[1]</a> A. Vehtari, A. Gelman, and J. Gabry, "Practical Bayesian model evaluation using leave-one-out cross-validation and WAIC," *Statistics and Computing*, vol. 27, no. 5, pp. 1413-1432, 2017.

<a id="2">[2]</a> R. D. Cook, "Detection of influential observation in linear regression," *Technometrics*, vol. 19, no. 1, pp. 15-18, 1977.

<a id="3">[3]</a> B. C. Wei, Y. Q. Hu, and W. K. Fung, "Generalized leverage and its applications," *Scandinavian Journal of Statistics*, vol. 25, no. 1, pp. 25-36, 1998.

---

[<- Previous: Model Estimation](index.md) | [Back to Index](../../index.md) | [Next: Analyses Overview ->](../analysis/overview.md)
