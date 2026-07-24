<!-- technical-reference-status: complete -->

# Exponential Distribution

[Distribution index](index.md) · [Mixed-data likelihood](../data-frame/index.md) · [Priors](../models/parameters-and-priors.md)

## Purpose and Parameterization

The two-parameter exponential model is appropriate for a nonnegative excess above a known or estimated lower endpoint when the hazard is constant. In RMC.Numerics the constructor order is $(\xi,\alpha)$: location $\xi$ has the units of the observation and scale $\alpha>0$ has the same units. If an excess $Y=X-\xi$ is modeled, $Y$ is memoryless; this is a strong tail assumption, not merely a convenient curve shape.

## Distribution

For $x\ge\xi$,

$$
f(x\mid\xi,\alpha)=\frac{1}{\alpha}\exp\!\left[-\frac{x-\xi}{\alpha}\right],\qquad
F(x)=1-\exp\!\left[-\frac{x-\xi}{\alpha}\right]. \tag{EXP.1}
$$

Below $\xi$, $f=F=0$. The survival and quantile functions are

$$
S(x)=\exp[-(x-\xi)/\alpha],\qquad
Q(p)=\xi-\alpha\log(1-p),\quad 0<p<1. \tag{EXP.2}
$$

The mean, standard deviation, median, mode, skewness, and ordinary kurtosis are $\xi+\alpha$, $\alpha$, $\xi+\alpha\log2$, $\xi$, $2$, and $9$. The upper tail is exponential and all positive moments exist.

## Likelihood and Bayesian Form

For exact values $x_i$, the log likelihood is

$$
\ell(\xi,\alpha)=-n\log\alpha-\alpha^{-1}\sum_i(x_i-\xi),
\quad \xi\le\min_i x_i. \tag{EXP.3}
$$

RMC.BestFit substitutes (EXP.1) into the common mixed-data likelihood: exact values contribute $\log f$; low outliers and left-censored values contribute $\log F$; right-censored values contribute $\log S$; intervals contribute $\log[F(u)-F(l)]$; uncertain values contribute the measurement-error convolution; and perception-period counts contribute powers of $F$ and $S$. For nonstationary models, $\xi_t$ and/or $\alpha_t$ are trend outputs at the observation index. The posterior is proportional to this full likelihood times all parameter and quantile-prior factors.

## Estimation and Numerical Behavior

`UnivariateDistribution.SetDefaultParameters` delegates initialization and natural-space bounds to the Numerics maximum-likelihood constraint routine using the exact sample, then creates uniform parameter priors on those returned bounds. These are data-dependent working bounds, not universal scientific limits. Scale proposals must remain positive; observations below the current location have zero probability and yield a rejected log-likelihood proposal. A free endpoint can be weakly identified by short samples and may sit close to the minimum observation.

The exponential model is the $\kappa=0$ limit of the RMC.Numerics generalized Pareto family. It should not be used for annual maxima merely because data are positive: block maxima generally require an extreme-value family, whereas exponential excesses arise naturally in peaks-over-threshold models.

## Compile-Checked API Example

The example uses discharge units consistently for location, scale, evaluation magnitude, and returned quantile. A 0.99 nonexceedance probability is an annual-exceedance probability of 0.01 only when the fitted random variable is an annual maximum.

<!-- snippet: distribution-exponential -->
```csharp
private static (double Density, double Cdf, double Quantile) EvaluateExponential()
{
    var distribution = new global::Numerics.Distributions.Exponential(1000.0, 500.0);
    return Evaluate(distribution, value: 1800.0, nonExceedanceProbability: 0.99);
}
```

## Validation and Limitations

The chapter formula is traced to `Numerics.Distributions.Exponential`; API compatibility is guarded by the compiled documentation fixture. Numerical validation should additionally test normalization, $F(Q(p))=p$, the memoryless identity, and the $\kappa\to0$ generalized-Pareto limit. No claim of hydrologic adequacy follows from those programmatic checks. Threshold choice, independence of excesses, exposure, and temporal stationarity must be assessed in the analysis using this family.

## References

<a id="ref-1"></a>[1] S. Coles, *An Introduction to Statistical Modeling of Extreme Values*. Springer, 2001.
