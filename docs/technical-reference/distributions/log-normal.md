<!-- technical-reference-status: complete -->

# Log-Normal Distribution (Base 10)

[Distribution index](index.md) · [Natural-Space Ln-Normal](ln-normal.md) · [Parameterization crosswalk](../appendices/parameterization-crosswalk.md)

## Purpose and Parameterization

`Numerics.Distributions.LogNormal` models $Y=\log_{10}X$ as Normal and uses constructor order $(\mu_Y,\sigma_Y)$. Both parameters are expressed in base-10 logarithmic units; they are not the arithmetic mean and standard deviation of $X$. BestFit's standard instance uses `Base = 10`. The mutable base is part of Numerics, but setting it below one clamps it to one, where the logarithm and Jacobian are undefined; BestFit documentation and examples therefore use base 10 only.

## Distribution

For $x>0$, write $y=\log_{10}x$, $z=(y-\mu_Y)/\sigma_Y$, and $c=\ln 10$. Then

$$
f_X(x)=\frac{\phi(z)}{x\sigma_Y c},\qquad
F_X(x)=\Phi(z),\qquad S_X(x)=1-\Phi(z). \tag{LN10.1}
$$

The factor $(x\ln10)^{-1}$ is the transformation Jacobian. The quantile is

$$
Q_X(p)=10^{\mu_Y+\sigma_Y\Phi^{-1}(p)}. \tag{LN10.2}
$$

If $a=\sigma_Y\ln10$, then

$$
E[X]=\exp(\mu_Y\ln10+a^2/2),\quad
\operatorname{Var}(X)=E[X]^2(e^{a^2}-1). \tag{LN10.3}
$$

All positive moments exist. Median is $10^{\mu_Y}$; the upper tail is lognormal.

## Likelihood and Inference

For exact positive values,

$$
\ell=-n\log\sigma_Y-n\log(\ln10)-\sum_i\log x_i
-\frac n2\log(2\pi)-\frac12\sum_i z_i^2. \tag{LN10.4}
$$

The Jacobian is essential for a likelihood on the original $X$ scale. RMC.BestFit's family methods supply the density and CDF to all exact, censored, uncertain, interval, threshold, stationary, and nonstationary components; there is no additional external transformation step. Trends act on $\mu_Y$ and $\sigma_Y$. The posterior multiplies the complete likelihood by parameter and quantile priors.

Initialization and working bounds are sample-dependent Numerics outputs and become default uniform-prior bounds. The source reports `MinimumOfParameters` as zero for $\mu_Y$, although a log-space mean is mathematically allowed to be negative; the data-dependent constraints and intended measurement units must therefore be reviewed for small-valued variables.

## Compile-Checked API Example

<!-- snippet: distribution-log-normal -->
```csharp
private static (double Density, double Cdf, double Quantile) EvaluateLogNormal()
{
    var distribution = new global::Numerics.Distributions.LogNormal(3.20, 0.20);
    return Evaluate(distribution, value: 2200.0, nonExceedanceProbability: 0.99);
}
```

The mean 3.20 and standard deviation 0.20 describe $\log_{10}$ discharge. This is categorically different from the natural-space arguments in `LnNormal`.

## Validation and Limitations

Closed-form verification uses a symmetric deterministic sample in base-10 log space. It verifies the population-divisor MLEs for $\mu_Y$ and $\sigma_Y$, the original-measure log likelihood including the $(x\ln 10)^{-1}$ Jacobian, equality of scalar and pointwise likelihood sums, the median CDF, and a selected analytical quantile. Parameter estimates agree within $10^{-5}$ and the maximized log likelihood within $10^{-8}$; direct likelihood identities use $10^{-10}$. Zero and negative observations have no support and must be represented through censoring or another family rather than silently transformed.

## References

<a id="ref-1"></a>[1] E. L. Crow and K. Shimizu, eds., *Lognormal Distributions: Theory and Applications*. Marcel Dekker, 1988.
