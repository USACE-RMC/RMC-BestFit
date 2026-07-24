<!-- technical-reference-status: complete -->

# Normal Distribution

[Distribution index](index.md) · [Generalized Normal](generalized-normal.md) · [Mixed-data likelihood](../data-frame/index.md)

## Purpose and Parameterization

The Normal model is a symmetric location-scale distribution for an unconstrained response. RMC.Numerics uses constructor order $(\mu,\sigma)$, where mean $\mu\in\mathbb R$ and standard deviation $\sigma>0$ have observation units. Direct Normal modeling of discharge assigns positive probability to negative values; that may be negligible for some data but is a structural assumption that must be checked.

## Distribution

With $z=(x-\mu)/\sigma$, standard Normal density $\phi$, and CDF $\Phi$,

$$
f(x\mid\mu,\sigma)=\frac{1}{\sigma}\phi(z)
=\frac{1}{\sigma\sqrt{2\pi}}e^{-z^2/2},\qquad
F(x)=\Phi(z). \tag{NOR.1}
$$

The survival and quantile functions are

$$
S(x)=1-\Phi(z),\qquad Q(p)=\mu+\sigma\Phi^{-1}(p). \tag{NOR.2}
$$

Mean, median, and mode equal $\mu$; variance is $\sigma^2$, skewness is zero, and ordinary kurtosis is $3$. All moments exist and both tails are unbounded with Gaussian decay.

## Likelihood and Posterior

For exact independent observations,

$$
\ell(\mu,\sigma)=-n\log\sigma-\frac{n}{2}\log(2\pi)
-\frac{1}{2\sigma^2}\sum_i(x_i-\mu)^2. \tag{NOR.3}
$$

The RMC.BestFit mixed-data likelihood uses $f$, $F$, and $S$ from (NOR.1)–(NOR.2) for exact, censored, uncertain, interval, and threshold information. Under nonstationarity, $\mu_i$ and/or $\sigma_i$ are trend-model predictions. The posterior combines every likelihood component with configured parameter and quantile priors. RMC.BestFit derives sample-dependent initialization and working bounds through the Numerics constraint routine; the mathematical constraints remain $\mu\in\mathbb R$ and $\sigma>0$.

The likelihood can drive $\sigma$ toward zero for degenerate data, so repeated or nearly identical observations require careful validation. A Normal fit can be useful for transformed data or approximately symmetric residuals, but direct rare-flood extrapolation is sensitive to the unbounded lower support and light upper tail.

## Compile-Checked API Example

<!-- snippet: distribution-normal -->
```csharp
private static (double Density, double Cdf, double Quantile) EvaluateNormal()
{
    var distribution = new global::Numerics.Distributions.Normal(1500.0, 400.0);
    return Evaluate(distribution, value: 2200.0, nonExceedanceProbability: 0.99);
}
```

All magnitudes use the same units. If the modeled quantity is annual maximum discharge, the analyst must still justify approximate independence, stationarity or covariate trends, and the Normal tail.

## Numerical Implementation and Validation

CDF and inverse-CDF calculations use the corresponding Numerics special-function paths. Required checks include $F(Q(p))\approx p$, symmetry, known moments, normalized density, and convergence of Pearson III and generalized Normal calculations as skew/shape approaches zero. The API example is compile-checked but intentionally reports no hand-written numerical output.

## References

<a id="ref-1"></a>[1] N. L. Johnson, S. Kotz, and N. Balakrishnan, *Continuous Univariate Distributions*, vol. 1, 2nd ed. Wiley, 1994.
