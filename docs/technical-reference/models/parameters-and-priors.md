<!-- technical-reference-status: complete -->

# Parameters, Bounds, and Priors

[<- Model contracts](overview.md) | [Technical reference index](../index.md) | [Next: Data likelihood ->](../data-frame/index.md)

## Purpose

This chapter defines how RMC.BestFit represents parameters and constructs Bayesian prior terms. It applies to the general `IModel` contract and gives the exact additional prior used by `UnivariateDistribution`. Specialized chapters document extra terms for time-series, rating-curve, bivariate, and spatial models. Bulletin 17C parameter and quantile *penalties* belong to its GMM objective and must not be described as Bayesian priors.

## Notation and spaces

| Symbol | Meaning |
|---|---|
| $\theta=(\theta_1,\ldots,\theta_p)$ | Ordered model parameter vector supplied to the likelihood |
| $[a_k,b_k]$ | Configured numerical bounds for $\theta_k$ |
| $\pi_k(\theta_k)$ | Configured marginal parameter-prior density |
| $\sigma(\theta)$ | Distribution scale parameter identified by the model |
| $\alpha_j$ | Annual exceedance probability (AEP) of quantile prior $j$ |
| $q_j(\theta)$ | Nonexceedance quantile $F^{-1}(1-\alpha_j\mid\theta)$ |
| $r_j$ | Expert-judgment density attached to a quantile or quantile difference |
| $D$ | Determinant returned by the distribution's quantile Jacobian |

Natural parameter space, trend-coefficient space, and link space are not interchangeable. `IModel` likelihood methods receive the model's ordered parameter vector. For a stationary univariate model, that vector is the Numerics distribution parameterization. For a nonstationary univariate model, it is the concatenation of trend-model coefficients, and the distribution parameters are predicted at each observation index. Link functions used elsewhere in the library have their own documented Jacobians; `ModelParameter` itself has no transform property.

## Parameter metadata and constraints

Each `ModelParameter` stores `OwnerName`, `Name`, `Value`, inclusive `LowerBound` and `UpperBound`, a Numerics `UnivariateDistributionBase` prior, `IsPositive`, and `IsFixed`. `Validate()` rejects inverted bounds, non-finite or out-of-bound values, invalid prior parameters, and a positive parameter whose prior support extends below the library's positive floor.

The controls have distinct meanings:

- Bounds delimit the numerical domain offered to optimization or sampling and are serialized.
- `IsPositive` strengthens validation of the prior support; it is not a logarithmic transform.
- `IsFixed` instructs an estimator to hold the parameter constant. The parameter remains present in the model vector and likelihood.
- The prior distribution contributes density even when it is flat over a broad finite interval. `UseDefaultFlatPriors` requests model-defined default priors; it does not mean “omit the prior.”

Before estimation, verify that bounds express defensible scientific constraints. A posterior or MLE accumulated at a bound is a boundary result, not evidence that the bound is correct.

## General parameter-prior contribution

The base prior contribution is the sum of the configured marginal log densities,

$$
\ell_{P,\mathrm{par}}(\theta)
=\sum_{k=1}^{p}\log\pi_k(\theta_k).
\tag{1}
$$

This form encodes prior independence among the stored parameter priors. Dependence may enter through a model-specific joint term, a quantile reparameterization, or a spatial hierarchy. If any marginal density is zero or a candidate violates support, the contribution is negative infinity.

Bounds are not normalization constants for an arbitrary prior. For example, assigning a Normal prior and narrower optimizer bounds produces a bounded search with a Normal density evaluated inside it; it does not automatically replace that prior by a properly normalized truncated Normal distribution.

## Scale-invariant prior option

When `UseJeffreysRuleForScale` is enabled and the model can identify a scale parameter $\sigma>0$, `UnivariateDistribution` adds

$$
\pi_J(\sigma)\propto\frac{1}{\sigma},
\qquad
\ell_{P,J}(\sigma)=-\log\sigma.
\tag{2}
$$

A non-positive scale returns negative infinity. Equation (2) is the implemented scale-invariant rule; it should not be generalized in reporting to a full joint Jeffreys prior for every distribution. The ordinary configured prior for the same parameter is also evaluated, so enabling the option multiplies that configured density by $1/\sigma$. Analysts must state both components.

Because Equation (2) is improper on $(0,\infty)$, posterior propriety depends on the likelihood and all other priors. A finite MCMC run does not prove propriety.

## Single quantile prior

Engineering judgment is often elicited more naturally for a rare flood quantile than for location, scale, and shape. With one `QuantilePrior` at AEP $\alpha$, RMC.BestFit evaluates the expert density $r$ at

$$
q_\alpha(\theta)=F^{-1}(1-\alpha\mid\theta),
\qquad
\ell_{P,Q}(\theta)=\log r\!\left(q_\alpha(\theta)\right).
\tag{3}
$$

`Alpha` is an exceedance probability: `Alpha = 0.01` means the 1% AEP, conventionally called the 100-year quantile under stationarity. It does not mean the first percentile. The expert distribution uses the same physical units as the modeled variable.

The code below configures a Normal flood model with a parameter prior, the scale-invariant term, and a Normal prior on the 1% AEP quantile. The values are illustrative; a real elicitation must document expert basis, units, dependence, and calibration.

<!-- snippet: priors-single-quantile -->
```cs
private static UnivariateDistribution ConfigureSingleQuantilePrior(global::RMC.BestFit.Models.DataFrame dataFrame)
{
    var model = new UnivariateDistribution(
        dataFrame,
        UnivariateDistributionType.Normal)
    {
        UseDefaultFlatPriors = false,
        UseJeffreysRuleForScale = true,
        EnableQuantilePriors = true,
        UseSingleQuantile = true,
        QuantilePriors = new List<QuantilePrior>
        {
            new QuantilePrior(0.01, new Normal(2500.0, 300.0))
        }
    };

    model.Parameters[0].PriorDistribution = new Normal(1400.0, 500.0);
    model.ProcessQuantilePriors();
    return model;
}
```

`ProcessQuantilePriors()` materializes the internal form used by likelihood evaluation. Call it after replacing the list programmatically.

## Multiple quantile priors

The multi-quantile option is available when `UseSingleQuantile` is false and the number of elicited quantiles equals the number of distribution parameters. The configured AEPs and expert quantile distributions must be ordered so rarer floods have larger quantiles. RMC.BestFit retains the first elicited distribution for $q_1$, but transforms later expert statements into distributions for positive increments

$$
\Delta q_j(\theta)=q_j(\theta)-q_{j-1}(\theta),
\qquad j=2,\ldots,p.
\tag{4}
$$

For configured expert means $m_j$ and variances $v_j$, `ProcessQuantilePriors()` computes

$$
m_{\Delta,j}=m_j-m_{j-1},
\qquad
s_{\Delta,j}=\sqrt{v_j+v_{j-1}},
\tag{5}
$$

then moment-matches a Numerics `GammaDistribution` to $(m_{\Delta,j},s_{\Delta,j})$. The variance sum corresponds to treating adjacent elicited quantiles as independent for this transformation. It does not preserve an elicited covariance structure.

The resulting contribution is

$$
\ell_{P,Q}(\theta)
=\log r_1(q_1)
+\sum_{j=2}^{p}\log r_j(\Delta q_j)
+\log|D(\theta)|.
\tag{6}
$$

Here $D$ is the determinant supplied by the distribution's `IStandardError.QuantileJacobian` implementation for probabilities $(1-\alpha_1,\ldots,1-\alpha_p)$. It converts the density specified in quantile coordinates to the distribution-parameter coordinates used by the posterior. A zero determinant returns negative infinity. This is a parameterization Jacobian, not a data-transformation Jacobian.

## Complete univariate prior

For a stationary univariate distribution, the implemented prior is therefore

$$
\ell_P(\theta)
=\ell_{P,\mathrm{par}}(\theta)
+I_J\ell_{P,J}(\theta)
+I_Q\ell_{P,Q}(\theta),
\tag{7}
$$

where $I_J$ and $I_Q$ indicate enabled and structurally valid options. The scalar and named pointwise-prior paths contain the same parameter, Jeffreys-scale, quantile, and quantile-Jacobian contributions. This reconciles the earlier consistency concern: in the current source, summing `PriorComponent.LogLikelihood` values agrees with `PriorLogLikelihood` for a valid finite state.

For a nonstationary univariate model, the candidate vector contains trend coefficients, and the parameter priors in Equation (1) apply directly to those coefficients. Distribution-dependent prior terms (the optional Jeffreys scale term, quantile-prior densities, and the quantile Jacobian) are evaluated using distribution parameters predicted at the last, most-recent index in `DataFrame.FullTimeSeries`. They are evaluated once at that reference time, not once per historical observation; `ParameterTimeIndex` remains reserved for prediction and display. This present-condition convention is consistent with the published quantile-prior workflow of Viglione et al. [2](#ref-2). It anchors an expert quantile statement to the final observed time step, so analysts must document that reference time when the distribution changes through time.

## Identifiability and prior sensitivity

- A rare-quantile prior can dominate a short systematic record, particularly when it constrains shape. Report analyses with and without the prior or otherwise quantify sensitivity.
- Parameter priors plus quantile priors can encode overlapping information. If both derive from the same regional study or expert panel, treating them as independent can count information twice.
- Multiple quantile priors require ordered, non-crossing expert distributions. The implementation validates increasing means and 5th/95th percentiles, but those checks do not establish expert calibration.
- A broad prior is not automatically weak on return levels. Nonlinear tail mappings can make apparently broad parameter priors informative in quantile space.
- An improper scale prior requires a proper posterior. Inspect tail behavior and sampler stability; do not rely on convergence diagnostics alone.
- Fixed parameters understate uncertainty unless their values are known without relevant error. Use a defensible prior when uncertainty exists.

## Implementation and evidence

| Concern | Implementation source | Evidence |
|---|---|---|
| Parameter state and validation | `src/RMC.BestFit/Models/Support/ModelParameter.cs` | `src/RMC.BestFit.Tests/Models/Support/ModelParameterTests.cs` |
| Quantile-prior representation | `src/RMC.BestFit/Models/Support/QuantilePrior.cs` | serialization and validation unit tests |
| Processing and full prior | `src/RMC.BestFit/Models/UnivariateDistribution/UnivariateDistribution.cs` | `src/RMC.BestFit.Verification/ModelEstimation/PointwiseLogLikelihoodTests.cs` and univariate verification tests |
| Published quantile-prior workflow | Viglione et al. data and `ViglioneEtAlTests.cs` | long-running verification project; not part of the fast gate |
| Compile-checked API | `src/RMC.BestFit.Tests/Documentation/Examples/ParameterPriorExamples.cs` | `TechnicalReferenceDocumentationTests` |

## References

<a id="ref-1"></a>[1] H. Jeffreys, "An invariant form for the prior probability in estimation problems," *Proceedings of the Royal Society A*, vol. 186, no. 1007, pp. 453-461, 1946. doi: 10.1098/rspa.1946.0056.

<a id="ref-2"></a>[2] A. Viglione, R. Merz, J. L. Salinas, and G. Blöschl, "Flood frequency hydrology: 3. A Bayesian analysis," *Water Resources Research*, vol. 49, no. 2, pp. 675-692, 2013. doi: 10.1029/2011WR010782.

<a id="ref-3"></a>[3] S. G. Coles and J. A. Tawn, "A Bayesian analysis of extreme rainfall data," *Journal of the Royal Statistical Society: Series C*, vol. 45, no. 4, pp. 463-478, 1996. doi: 10.2307/2986068.

[<- Model contracts](overview.md) | [Technical reference index](../index.md) | [Next: Data likelihood ->](../data-frame/index.md)
