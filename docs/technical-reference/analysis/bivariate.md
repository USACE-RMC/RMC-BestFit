# Bivariate Analysis

[<- Previous: Composite Analysis](composite.md) | [Back to Index](../../index.md) | [Next: Coincident Frequency ->](coincident-frequency.md)

`BivariateAnalysis` fits a `BivariateDistribution` consisting of two univariate marginals and a copula. It is used for joint frequency, conditional frequency, and as the upstream input to coincident frequency analysis.

## Public API

| API | Purpose |
|-----|---------|
| `BivariateDistribution(IUnivariateModel, IUnivariateModel, CopulaType)` | Creates the joint model |
| `BivariateDistribution.CreateCopula(...)` | Creates supported Numerics copulas |
| `BivariateDistribution.DataLogLikelihood(...)` | Joint likelihood |
| `BivariateDistribution.PointwiseDataLogLikelihood(...)` | WAIC/LOO-CV support |
| `BivariateDistribution.GenerateRandomValues(...)` | Joint simulation |
| `BivariateAnalysis(BivariateDistribution)` | Analysis workflow |
| `BivariateAnalysis.BayesianAnalysis` | MCMC settings and results |
| `BivariateAnalysis.AnalysisResults` | Joint-frequency uncertainty results |
| `BivariateAnalysis.CreateFrequencyAnalysisResultsAsync()` | Reprocesses frequency outputs |

## Usage Pattern

```cs
using Numerics.Distributions;
using Numerics.Distributions.Copulas;
using RMC.BestFit;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;

var xData = new DataFrame { ExactSeries = new ExactSeries(new[] { 10.0, 12.0, 14.0, 16.0 }) };
var yData = new DataFrame { ExactSeries = new ExactSeries(new[] { 5.0, 7.0, 9.0, 11.0 }) };

var marginalX = new UnivariateDistribution(xData, UnivariateDistributionType.Normal);
var marginalY = new UnivariateDistribution(yData, UnivariateDistributionType.Gumbel);

var model = new BivariateDistribution(marginalX, marginalY, CopulaType.Normal);
var analysis = new BivariateAnalysis(model);

if (analysis.Validate().IsValid)
{
    await analysis.RunAsync();
}
```

## Relationship To Coincident Frequency

`BivariateAnalysis` estimates the joint probability model for variables `X` and `Y`. [Coincident Frequency](coincident-frequency.md) combines that fitted joint model with a response surface `Z = f(X, Y)`.

## Source-Verified Likelihood

`BivariateDistribution.DataLogLikelihood(...)` estimates only the copula parameters. The marginal distributions are supplied by the two `IUnivariateModel` inputs. In pseudo-likelihood mode, BestFit evaluates the copula log density directly on pseudo-uniform sample pairs. In inference-from-margins mode, it transforms each raw pair through the marginal CDFs and then evaluates the copula log density. Non-finite or exception-producing likelihood evaluations return `double.NegativeInfinity`.

`PointwiseDataLogLikelihood(...)` follows the same path one pair at a time for WAIC, LOO-CV, and influence diagnostics.

## Implementation Sources

Primary source paths: `src/RMC.BestFit/Models/BivariateDistribution/BivariateDistribution.cs`, `src/RMC.BestFit/Analyses/Bivariate/BivariateAnalysis.cs`, and `src/RMC.BestFit/Analyses/Bivariate/CoincidentFrequencyAnalysis.cs`.

---

[<- Previous: Composite Analysis](composite.md) | [Back to Index](../../index.md) | [Next: Coincident Frequency ->](coincident-frequency.md)
