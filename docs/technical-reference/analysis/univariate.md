# Univariate Analysis

[<- Previous: Distribution Fitting](distribution-fitting.md) | [Back to Index](../../index.md) | [Next: Composite Analysis ->](composite.md)

`UnivariateAnalysis` runs Bayesian frequency analysis for a single `UnivariateDistribution`. It owns sampler settings, probability ordinates, posterior results, and derived frequency results.

## Public API

| Member | Purpose |
|--------|---------|
| `UnivariateAnalysis(UnivariateDistribution)` | Creates an analysis for a model |
| `BayesianAnalysis` | MCMC settings, posterior samples, DIC, WAIC, LOOIC |
| `ProbabilityOrdinates` | Annual exceedance probabilities used for outputs |
| `AnalysisResults` | Frequency-analysis uncertainty results |
| `ChronologyAnalysisResults` | Nonstationary chronology results |
| `RunAsync(...)` | Runs MCMC and post-processing |
| `CreateFrequencyAnalysisResultsAsync()` | Recomputes frequency results from posterior output |
| `Validate()` | Checks model and analysis state |
| `ToXElement()` | Serializes settings and result metadata |

## Workflow

```cs
using Numerics.Distributions;
using RMC.BestFit;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;

var dataFrame = new DataFrame
{
    ExactSeries = new ExactSeries(new[] { 1010.0, 1230.0, 1560.0, 1780.0 })
};

var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.LogPearsonTypeIII);
var analysis = new UnivariateAnalysis(model);

analysis.BayesianAnalysis.Iterations = 3000;
analysis.BayesianAnalysis.WarmupIterations = 1500;

if (analysis.Validate().IsValid)
{
    await analysis.RunAsync();
}
```

## Result Access

```cs
var results = analysis.BayesianAnalysis.Results;
if (results is not null)
{
    var map = results.MAP.Values;
    model.SetParameterValues(map);

    double onePercentAepQuantile = model.Distribution.InverseCDF(0.99);
    Console.WriteLine(onePercentAepQuantile);
}
```

## Related Analyses

| Analysis | Use |
|----------|-----|
| `Bulletin17CAnalysis` | Specialized Bulletin 17C workflow |
| `MixtureAnalysis` | Latent-population mixture model |
| `CompetingRiskAnalysis` | Multiple process max/min model |
| `PointProcessAnalysis` | Peaks-over-threshold model |
| `CompositeAnalysis` | Composite or model-averaged distribution |

## Implementation Sources

Primary source paths: `src/RMC.BestFit/Analyses/Univariate/UnivariateAnalysis.cs`, `src/RMC.BestFit/Analyses/Univariate/UncertaintyAnalysisResults.cs`, `src/RMC.BestFit/Models/UnivariateDistribution/UnivariateDistribution.cs`, and `src/RMC.BestFit/Estimation/BayesianAnalysis.cs`.

---

[<- Previous: Distribution Fitting](distribution-fitting.md) | [Back to Index](../../index.md) | [Next: Composite Analysis ->](composite.md)
