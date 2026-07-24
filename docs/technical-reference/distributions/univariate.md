<!-- technical-reference-status: complete -->

# Common Univariate Distribution Model

[Distribution index](index.md) · [Data likelihood](../data-frame/index.md) · [Parameters and priors](../models/parameters-and-priors.md)

`RMC.BestFit.Models.UnivariateDistribution` turns one of the fifteen Numerics families into an `IModel`. The Numerics object supplies $f$, $F$, $S$, and $Q$; BestFit supplies observation-process likelihoods, trend coefficients, priors, pointwise decomposition, simulation, validation, and serialization.

## Construction and Parameter Order

The default constructor creates a Log-Pearson Type III family. Other constructors accept a `DataFrame` plus either a `UnivariateDistributionType` or an existing `Numerics.Distributions.UnivariateDistributionBase`. In a stationary model, the fitted vector follows `Distribution.GetParameters` order exactly. One `ConstantTrend` holds each family parameter. In a nonstationary model, the vector is the concatenation of every trend's coefficients in family-parameter order.

The authoritative family order is listed in the [distribution index](index.md). In particular, `LnNormal` coefficients are natural-space moments, `LogNormal` and `LogPearsonTypeIII` coefficients are base-10 log moments, and GEV/GPD shape signs are opposite the common Coles convention.

## Complete Observation Likelihood

Let $E,U,I,T$ denote exact, uncertain, interval, and threshold records. For model parameters $\theta$ and a low-outlier threshold $c_L$, the stationary likelihood implemented by the model is

$$
L_D(\theta)=
\prod_{i\in E_0}f(x_i\mid\theta)
\prod_{i\in E_L}F(c_L\mid\theta)
\prod_{i\in U}\int g_i(q)f(q\mid\theta)\,dq
\prod_{i\in I}[F(u_i\mid\theta)-F(l_i\mid\theta)]
\prod_{j\in T}F(c_j\mid\theta)^{b_j}S(c_j\mid\theta)^{a_j}. \tag{UNI.1}
$$

Here $E_0$ are ordinary exact values, $E_L$ are exact values flagged as low outliers, $g_i$ is the measurement-error density, and $(b_j,a_j)$ are below/above threshold counts. The uncertain integral uses a 20-point Gauss-Legendre rule over the central $1-2\times10^{-8}$ mass of $g_i$ and divides by that retained mass. Threshold counts omit the binomial coefficient, which is constant for a fixed record.

The scalar data log likelihood is the logarithm of (UNI.1). Impossible support, nonpositive interval probability, invalid distribution parameters, or any non-finite contribution returns negative infinity. The posterior target is

$$
\log p(\theta\mid D)=\log L_D(\theta)+\log p(\theta), \tag{UNI.2}
$$

where the prior term includes configured coefficient priors and optional Jeffreys-scale and quantile-prior contributions.

## Nonstationarity

For parameter $k$ with trend $h_k(t;\beta_k)$, the distribution at index $t_i$ is

$$
\theta_i=(h_1(t_i;\beta_1),\ldots,h_K(t_i;\beta_K)). \tag{UNI.3}
$$

The model clones trends, assigns the candidate coefficient slices, predicts every natural distribution parameter, validates the resulting Numerics family, and evaluates that observation. Exact, uncertain, and interval records retain their explicit indexes. Perception-period counts are expanded into index-level pseudo-observations using `FullTimeSeries`; below events are assigned from the beginning and above events from the end after explicit records are removed. That deterministic chronology assumption is a documented limitation when parameters vary within a period.

## Defaults, Bounds, and Priors

`SetDefaultParameters` passes exact sample values to the selected Numerics `GetParameterConstraints` implementation. Returned initial, lower, and upper arrays initialize each constant coefficient and its default uniform prior. These are data-dependent numerical working bounds—not the family's mathematical support and not evidence-based prior limits. Nonstationary slope bounds are scaled using the record index range.

`ModelParameter` stores value, bounds, fixed state, and prior; it does not itself transform coordinates. Scale positivity is maintained by bounds and distribution validation. `UseDefaultFlatPriors`, `UseJeffreysRuleForScale`, `EnableQuantilePriors`, and `QuantilePriors` alter the prior target as detailed in the prior chapter.

## Pointwise Likelihood and Diagnostics

For stationary models, pointwise output contains one item for each exact, uncertain, or interval record and one aggregate item per threshold period. For nonstationary models, threshold components follow the expanded index sequence. `PointwiseDataLogLikelihoodComponents` attaches type, value, count, and name metadata. The values sum to the scalar data log likelihood for a finite state and feed WAIC, PSIS-LOO, and influence diagnostics. Comparing models with different pointwise partitions is not valid.

## Simulation and Validation

`GenerateRandomValues` simulates through the selected Numerics family after parameters are assigned. Simulation represents the latent fitted random variable; it does not automatically recreate historical thresholds, measurement error, missingness, or censoring. `Validate` checks data/model state and returns messages; analysis classes perform lifecycle validation before estimation.

Failure modes include insufficient exact data for default constraints, parameter-dependent support excluding observations, degenerate scales, weak tail-shape identification, incompatible nonstationary predictions, multimodal likelihoods, and overconfident extrapolation. An optimizer result or apparently converged chain does not establish family adequacy.

## Compile-Checked Construction

<!-- snippet: data-frame-mixed-observations -->
```csharp
private static global::RMC.BestFit.Models.DataFrame CreateMixedDataFrame()
{
    var threshold = new ThresholdData(1900, 1949, 1000.0)
    {
        NumberAbove = 3
    };

    return new global::RMC.BestFit.Models.DataFrame
    {
        ExactSeries = new ExactSeries(new[]
        {
            new ExactData(1950, 1210.0),
            new ExactData(1951, 1675.0)
        }),
        UncertainSeries = new UncertainSeries(new[]
        {
            new UncertainData(1890, new Normal(1550.0, 180.0))
        }),
        IntervalSeries = new IntervalSeries(new[]
        {
            new IntervalData(1880, 1100.0, 1300.0, 1500.0)
        }),
        ThresholdSeries = new ThresholdSeries(new[] { threshold })
    };
}
```

This fixture demonstrates valid production data types. Full estimation belongs to an analysis workflow, not a direct call to an estimator on a Numerics distribution.

## Implementation and Evidence

Primary implementation symbols are `UnivariateDistribution`, `UnivariateDistributionModelBase`, `DataFrame`, `ModelParameter`, and the selected Numerics distribution class. The compile-checked fixture guards the current construction API. Numerical validation claims remain family-specific and are linked from each chapter; long-running verification is never inferred from compilation alone.

## References

<a id="ref-1"></a>[1] S. Coles, *An Introduction to Statistical Modeling of Extreme Values*. Springer, 2001.

<a id="ref-2"></a>[2] A. Gelman et al., *Bayesian Data Analysis*, 3rd ed. CRC Press, 2013.

---

[Distribution index](index.md)
