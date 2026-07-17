# Coincident Frequency Analysis

[<- Previous: Bivariate Analysis](bivariate.md) | [Back to Index](../../index.md) | [Next: Rating Curve ->](rating-curve.md)

`CoincidentFrequencyAnalysis` computes a response-frequency curve from a fitted `BivariateAnalysis` and an externally supplied response surface `Z = f(X, Y)`.

## Public API

| Member | Purpose |
|--------|---------|
| `CoincidentFrequencyAnalysis()` | Empty analysis |
| `CoincidentFrequencyAnalysis(BivariateAnalysis, double[], double[], double[,])` | Creates an analysis from a fitted bivariate model and response grid |
| `BivariateAnalysis` | Upstream fitted bivariate model |
| `XValues` | Primary ordinates; length equals response rows |
| `YValues` | Secondary ordinates; length equals response columns |
| `BivariateResponse` | `Z[i,j]` response surface |
| `NumberOfBins` | Number of output `Z` bins |
| `MarginalXChain` | Optional posterior samples for X marginal |
| `MarginalYChain` | Optional posterior samples for Y marginal |
| `RunAsync(...)` | Computes response-frequency uncertainty |
| `SetZOutputValues(...)` | Overrides output bins |
| `ToXElement()` | Serializes configuration and output |

## Source-Verified Algorithm

The analysis integrates over columns of the response surface using the fitted bivariate copula. For each output level `z` and each `Y` interval, BestFit interpolates the `X` value that produces `z`, evaluates a two-point copula CDF difference, and sums the contributions.

The implementation first forms `Y` bin edges as midpoints between adjacent `YValues`, with negative infinity at the first edge and positive infinity at the last edge. In copula space, this means `v_0 = 0`, `v_N = 1`, and each interior edge is `F_Y((y_{j-1}+y_j)/2)`.

```math
F_Z(z) = \sum_j \left[
C(u^*_{z,j}, v_{j+1}) -
C(u^*_{z,j}, v_j)
\right]
```

The `u^*_{z,j}` term is found by interpolating linearly in `(response, zeta)` space, where `zeta = Normal.StandardInverseCDF(F_X(x))`. When `z` falls outside a response column, BestFit linearly extrapolates from the nearest segment. Boundary identities `C(u,0)=0` and `C(u,1)=u` are used to avoid passing exact 0 or 1 into copula methods that internally apply normal quantiles.

The annual exceedance probability is `1 - F_Z(z)`. Posterior uncertainty loops over copula posterior draws from the fitted `BivariateAnalysis`. Optional `MarginalXChain` and `MarginalYChain` posterior samples vary the marginals by matched draw index; if marginal chains are absent, the point-estimate marginals are used.

## Usage Pattern

```cs
using RMC.BestFit.Analyses;

double[] xValues = { 10.0, 20.0, 30.0 };
double[] yValues = { 1.0, 2.0, 3.0 };
double[,] response =
{
    { 11.0, 12.0, 13.0 },
    { 21.0, 22.0, 23.0 },
    { 31.0, 32.0, 33.0 }
};

var coincident = new CoincidentFrequencyAnalysis(
    bivariateAnalysis,
    xValues,
    yValues,
    response);

coincident.NumberOfBins = 50;

if (coincident.Validate().IsValid)
{
    await coincident.RunAsync();
}
```

## Input Requirements

| Input | Requirement |
|-------|-------------|
| `BivariateAnalysis` | Estimated before running coincident analysis |
| `XValues` | Strictly ascending |
| `YValues` | Strictly ascending |
| `BivariateResponse` | Dimensions match X/Y arrays and increase along both axes |
| Posterior chains | Optional; if absent, point-estimate marginals are used |

## Implementation Sources

Primary source paths: `src/RMC.BestFit/Analyses/Bivariate/CoincidentFrequencyAnalysis.cs`, `src/RMC.BestFit/Analyses/Bivariate/BivariateAnalysis.cs`, and `src/RMC.BestFit/Models/BivariateDistribution/BivariateDistribution.cs`.

## References

<a id="1">[1]</a> R. B. Nelsen, *An Introduction to Copulas*, 2nd ed. New York, NY, USA: Springer, 2006.

---

[<- Previous: Bivariate Analysis](bivariate.md) | [Back to Index](../../index.md) | [Next: Rating Curve ->](rating-curve.md)
