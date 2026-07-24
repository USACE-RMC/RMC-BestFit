<!-- technical-reference-status: complete -->

# Ln-Normal Distribution

[Distribution index](index.md) · [Base-10 Log-Normal](log-normal.md) · [Parameterization crosswalk](../appendices/parameterization-crosswalk.md)

## Purpose and Public Parameterization

`Numerics.Distributions.LnNormal` is a natural-lognormal family whose public constructor and `GetParameters` use the **natural-space mean and standard deviation**, $(m,s)$, not the mean and standard deviation of $\ln X$. Both are in observation units and must be positive for a meaningful lognormal model. This differs intentionally from `LogNormal`, whose public parameters are log-space moments.

The constructor converts $(m,s)$ to internal Normal parameters

$$
\sigma_{\ln}^2=\log\left[1+(s/m)^2\right],\qquad
\mu_{\ln}=\log m-\frac12\sigma_{\ln}^2. \tag{LN.1}
$$

The public `Mu` and `Sigma` properties expose these internal log-space values, while `GetParameters`, `ParametersToString`, `Mean`, and `StandardDeviation` expose or return natural-space moments. Code that mixes these surfaces can silently change parameterization.

## Distribution

For $x>0$,

$$
f(x)=\frac{1}{x\sigma_{\ln}\sqrt{2\pi}}
\exp\left[-\frac{(\ln x-\mu_{\ln})^2}{2\sigma_{\ln}^2}\right],\qquad
F(x)=\Phi\left(\frac{\ln x-\mu_{\ln}}{\sigma_{\ln}}\right). \tag{LN.2}
$$

The survival is $1-F$ and

$$
Q(p)=\exp[\mu_{\ln}+\sigma_{\ln}\Phi^{-1}(p)]. \tag{LN.3}
$$

All positive moments exist, with $E[X^r]=\exp(r\mu_{\ln}+r^2\sigma_{\ln}^2/2)$. Median and mode are $e^{\mu_{\ln}}$ and $e^{\mu_{\ln}-\sigma_{\ln}^2}$. The upper tail is unbounded and heavier than an exponential tail but lighter than a power law.

## Likelihood, Priors, and Estimation

For exact positive values,

$$
\ell=-n\log\sigma_{\ln}-\sum_i\log x_i-\frac n2\log(2\pi)
-\frac{1}{2\sigma_{\ln}^2}\sum_i(\ln x_i-\mu_{\ln})^2. \tag{LN.4}
$$

RMC.BestFit uses natural-space $(m,s)$ as model coefficients and passes them through the constructor conversion before evaluating exact, censored, uncertain, interval, and threshold likelihood components. Quantile priors are evaluated from (LN.3). Sample-dependent starts and working bounds come from the Numerics constraint routine. The direct-moment conversion guards only nonpositive standard deviation explicitly; nonpositive or near-zero mean can lead to non-finite internal values and must be rejected during data/model validation.

When parameters vary with covariates, the trends predict natural-space mean and standard deviation, not $(\mu_{\ln},\sigma_{\ln})$. This distinction changes both interpretation and the nonlinear response to covariates.

## Compile-Checked API Example

<!-- snippet: distribution-ln-normal -->
```csharp
private static (double Density, double Cdf, double Quantile) EvaluateLnNormal()
{
    var distribution = new global::Numerics.Distributions.LnNormal(1500.0, 500.0);
    return Evaluate(distribution, value: 2200.0, nonExceedanceProbability: 0.99);
}
```

Here 1,500 and 500 are the arithmetic mean and standard deviation in discharge units. Passing log-space values would define a different and usually nonsensical model.

## Validation and Limitations

Required evidence includes round-trip recovery of $(m,s)$ after conversion, density normalization, CDF/quantile inversion, theoretical natural-space moments, and comparison with a natural-lognormal reference. The compiled fixture guards current constructor semantics. Positive support can be appropriate for discharge, but lognormal tail extrapolation remains a substantive assumption and may underrepresent heavy flood-generating mechanisms.

## References

<a id="ref-1"></a>[1] E. L. Crow and K. Shimizu, eds., *Lognormal Distributions: Theory and Applications*. Marcel Dekker, 1988.
