# Univariate Distributions

[<- Previous: Distribution API](index.md) | [Back to Index](../../index.md) | [Next: Mixture Models ->](mixture.md)

`UnivariateDistribution` is the primary model for fitting one probability distribution to a `DataFrame`. It supports exact, uncertain, interval, and threshold observations through `DataLogLikelihood`, prior terms through `PriorLogLikelihood`, and pointwise likelihoods for WAIC/LOO-CV diagnostics.

## Public API

| Member | Purpose |
|--------|---------|
| `UnivariateDistribution()` | Creates a default Log-Pearson Type III model |
| `UnivariateDistribution(DataFrame, UnivariateDistributionBase)` | Wraps an existing Numerics distribution |
| `UnivariateDistribution(DataFrame, UnivariateDistributionType)` | Creates a distribution by enum |
| `DataFrame` | Input observations |
| `Distribution` | Underlying Numerics distribution |
| `Parameters` | Estimation parameters, bounds, priors, and fixed flags |
| `UseDefaultFlatPriors` | Applies broad uniform priors |
| `UseJeffreysRuleForScale` | Uses Jeffreys-style scale priors |
| `EnableQuantilePriors` | Adds engineering-judgment quantile prior penalties |
| `IsNonstationary` | Enables parameter trend models |
| `SetParameterValues(...)` | Pushes parameter values into the model and distribution |
| `GenerateRandomValues(...)` | Simulates from the fitted model |
| `Validate()` | Returns validation status and messages |

## Likelihood Decomposition

```math
\log p(\theta | y) =
\log p(y | \theta) + \log p(\theta)
```

`DataLogLikelihood` computes the first term and `PriorLogLikelihood` computes the second. `LogLikelihood` returns their sum.

The source implementation evaluates the data term by observation type. Exact observations contribute `Distribution.LogLikelihood(value)` except low-outlier exact observations, which are converted to left-censored likelihood at `DataFrame.LowOutlierThreshold`. Uncertain observations integrate the product of the measurement-error density and the fitted distribution density over the central probability mass of the uncertainty distribution. Interval observations use `LogLikelihood_Intervals(lower, upper)`. Threshold records add below-threshold and above-threshold count contributions.

For nonstationary models, `SetParameterValues(...)` updates trend-model coefficients and evaluates each trend at the data index before evaluating the distribution likelihood. `PointwiseDataLogLikelihood(...)` mirrors the same component decomposition for WAIC, LOO-CV, and influence diagnostics.

## Example: MLE Then Bayesian Analysis

```cs
using Numerics.Distributions;
using RMC.BestFit;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

var dataFrame = new DataFrame
{
    ExactSeries = new ExactSeries(new[] { 1100.0, 1250.0, 1430.0, 1710.0, 1620.0 })
};

var model = new UnivariateDistribution(
    dataFrame,
    UnivariateDistributionType.GeneralizedExtremeValue);

var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
if (mle.Estimate())
{
    model.SetParameterValues(mle.BestParameterSet.Values);
}

var analysis = new UnivariateAnalysis(model);
analysis.BayesianAnalysis.Iterations = 3000;
analysis.BayesianAnalysis.WarmupIterations = 1500;
await analysis.RunAsync();
```

## Quantile Priors

Quantile priors add prior penalties on derived quantiles rather than raw distribution parameters. They are useful when engineering judgment is naturally stated as a flow estimate at an annual exceedance probability.

| Type | Public API |
|------|------------|
| `QuantilePrior` | Stores probability, expected quantile, uncertainty, and enabled state |
| `IQuantilePriors` | Interface for models exposing quantile priors |
| `QuantilePenalty` | Prior component used during likelihood evaluation |

## Nonstationary Models

When `IsNonstationary` is enabled, distribution parameters can be represented by trend functions. Trend and link functions are documented in [Trend and Link Functions](../support/trend-and-link-functions.md).

## Implementation Sources

Primary source paths: `src/RMC.BestFit/Models/UnivariateDistribution/UnivariateDistribution.cs`, `src/RMC.BestFit/Models/UnivariateDistribution/UnivariateDistributionModelBase.cs`, `src/RMC.BestFit/Models/UnivariateDistribution/QuantilePrior.cs`, `src/RMC.BestFit/Models/Support/ModelParameter.cs`, and `src/RMC.BestFit/Models/TrendFunctions`.

## References

<a id="1">[1]</a> S. Coles, *An Introduction to Statistical Modeling of Extreme Values*. London, U.K.: Springer, 2001.

<a id="2">[2]</a> Interagency Advisory Committee on Water Data, *Guidelines for Determining Flood Flow Frequency, Bulletin 17C*, U.S. Geological Survey, 2019.

---

[<- Previous: Distribution API](index.md) | [Back to Index](../../index.md) | [Next: Mixture Models ->](mixture.md)
