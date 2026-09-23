# Getting Started

[Back to Index](index.md) | [Next: Models Overview ->](technical-reference/models/overview.md)

This guide shows the minimum namespaces and workflows needed to use `RMC.BestFit.dll` from a .NET application.

## Installation

Reference the BestFit model library and its Numerics dependency from your application:

```xml
<ProjectReference Include="..\RMC.BestFit\RMC.BestFit.csproj" />
```

BestFit uses Numerics for probability distributions, optimization, MCMC, and matrix operations.

## Required Namespaces

```cs
using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Diagnostics;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
```

`RMC.BestFit` contains observation and series types such as `ExactSeries`, while `RMC.BestFit.Models` contains `DataFrame` and model classes.

## Create Input Data

```cs
using RMC.BestFit.Models;

var dataFrame = new DataFrame
{
    ExactSeries = new ExactSeries(new[] { 22.0, 27.4, 31.8, 29.1, 35.7 })
};
```

For censored or uncertain records, use `UncertainSeries`, `IntervalSeries`, and `ThresholdSeries`.

## Fit One Distribution With MLE

```cs
using Numerics.Distributions;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

var dataFrame = new DataFrame
{
    ExactSeries = new ExactSeries(new[] { 22.0, 27.4, 31.8, 29.1, 35.7 })
};

var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);
var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);

if (mle.Estimate())
{
    model.SetParameterValues(mle.BestParameterSet.Values);
    Console.WriteLine($"Log-likelihood: {mle.MaximumLogLikelihood:F3}");
}
```

## Run Bayesian Analysis

```cs
using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;

var dataFrame = new DataFrame
{
    ExactSeries = new ExactSeries(new[] { 22.0, 27.4, 31.8, 29.1, 35.7 })
};

var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.LogPearsonTypeIII);
var analysis = new UnivariateAnalysis(model);

analysis.BayesianAnalysis.Iterations = 3000;
analysis.BayesianAnalysis.WarmupIterations = 1500;

await analysis.RunAsync();

Console.WriteLine($"DIC: {analysis.BayesianAnalysis.DIC:F2}");
Console.WriteLine($"WAIC: {analysis.BayesianAnalysis.WAIC:F2}");
```

## Next Steps

Read [Models Overview](technical-reference/models/overview.md) for the model contract, then [Input Data Frame](technical-reference/data-frame/index.md) for flood-frequency data types.

---

[Back to Index](index.md) | [Next: Models Overview ->](technical-reference/models/overview.md)
