# Composite Analysis

[<- Previous: Univariate Analysis](univariate.md) | [Back to Index](../../index.md) | [Next: Bivariate Analysis ->](bivariate.md)

`CompositeAnalysis` combines estimated univariate analyses. It is the analysis-layer API for competing risks, mixtures, and model averaging.

## Public API

| Member | Purpose |
|--------|---------|
| `CompositeAnalysis()` | Empty composite |
| `CompositeAnalysis(IEnumerable<WeightedUnivariateAnalysis>)` | Composite from weighted analyses |
| `Analyses` | `ObservableCollection<WeightedUnivariateAnalysis>` |
| `CompositeDistributionType` | `CompetingRisks`, `Mixture`, or `ModelAverage` |
| `ModelAverageMethod` | AIC, BIC, DIC, WAIC, LOOIC, Equal, or RMSE |
| `Dependency` | Dependency assumption used in probability composition |
| `IsMaximum` | Max/min flag for competing-risks composition |
| `RunAsync(...)` | Computes composite uncertainty results |
| `ToXElement()` | Serializes configuration |

## Three Composition Modes

| Mode | Select With | Weight Meaning |
|------|-------------|----------------|
| Competing risks | `CompositeType.CompetingRisks` | Process contribution to max/min outcome |
| Mixture | `CompositeType.Mixture` | Latent population probability |
| Model averaging | `CompositeType.ModelAverage` | Information-criterion or equal model weight |

## Example: WAIC Model Averaging

```cs
using RMC.BestFit.Analyses;

var composite = new CompositeAnalysis(new[]
{
    new WeightedUnivariateAnalysis(logPearsonAnalysis, 0.0),
    new WeightedUnivariateAnalysis(gevAnalysis, 0.0)
});

composite.CompositeDistributionType = CompositeType.ModelAverage;
composite.ModelAverageMethod = AverageMethod.WAIC;

if (composite.Validate().IsValid)
{
    await composite.RunAsync();
}
```

For model averaging, the analysis estimates weights from the selected criterion. For mixture and competing-risks workflows, manually supplied weights must be meaningful for the process being modeled.

## Source-Verified Behavior

`CompositeAnalysis.RunAsync(...)` requires every child `IUnivariateAnalysis` to already be estimated and to have non-null `AnalysisResults`. It then recomputes model-average weights when needed and builds a realization array whose length is the minimum posterior-output length across all child analyses.

For `CompositeType.CompetingRisks`, each realization is a `Numerics.Distributions.CompetingRisks` distribution formed from the child realization at the same index. For `CompositeType.Mixture` and `CompositeType.ModelAverage`, each realization is a `Numerics.Distributions.Mixture` with the current child weights. The frequency results are created by `BootstrapAnalysis.Estimate(...)` over the composite realization array.

`AverageMethod.DIC`, `AverageMethod.WAIC`, and `AverageMethod.LOOIC` are rejected when any child is a `Bulletin17CAnalysis`, because B17C is fit by GMM and does not define those MCMC information criteria.

## Failure Modes

| Condition | Result |
|-----------|--------|
| A child analysis is not estimated | `Validate()` returns invalid |
| A child is itself `CompositeAnalysis` | `WeightedUnivariateAnalysis` rejects it |
| Information criterion is missing | Model-average weights cannot be estimated for that method |
| Weights are invalid for a mixture | Validation reports the configuration issue |

## Implementation Sources

Primary source paths: `src/RMC.BestFit/Analyses/Univariate/CompositeAnalysis.cs`, `src/RMC.BestFit/Analyses/Univariate/WeightedUnivariateAnalysis.cs`, and `src/RMC.BestFit/Analyses/Univariate/BootstrapAnalysis.cs`.

---

[<- Previous: Univariate Analysis](univariate.md) | [Back to Index](../../index.md) | [Next: Bivariate Analysis ->](bivariate.md)
