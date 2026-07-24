<!-- technical-reference-status: complete -->

# Gumbel Distribution

[Distribution index](index.md) · [GEV](generalized-extreme-value.md) · [Mixed-data likelihood](../data-frame/index.md)

## Purpose and Parameterization

The Gumbel distribution is the zero-shape extreme-value model for block maxima. RMC.Numerics uses location $\xi\in\mathbb R$ and scale $\alpha>0$, in constructor order $(\xi,\alpha)$. Both carry the units of the observation. It is suitable when the fitted upper tail is approximately exponential in the extreme-value sense; it should not be selected solely because its two parameters make estimation stable.

## Distribution

With $z=(x-\xi)/\alpha$ and support $x\in\mathbb R$,

$$
F(x)=\exp[-e^{-z}],\qquad
f(x)=\alpha^{-1}\exp[-z-e^{-z}],\qquad
S(x)=1-F(x). \tag{GUM.1}
$$

The quantile is

$$
Q(p)=\xi-\alpha\log[-\log p],\quad 0<p<1. \tag{GUM.2}
$$

Writing $\gamma_E$ for Euler's constant,

$$
E[X]=\xi+\gamma_E\alpha,\quad
\operatorname{Var}(X)=\frac{\pi^2\alpha^2}{6},\quad
\operatorname{mode}(X)=\xi. \tag{GUM.3}
$$

Skewness is $12\sqrt6\,\zeta(3)/\pi^3$ and ordinary kurtosis is $5.4$. The upper tail is unbounded and exponential; the lower tail decays double-exponentially.

## Likelihood and Inference

For exact independent block maxima,

$$
\ell(\xi,\alpha)=-n\log\alpha-\sum_i z_i-\sum_i e^{-z_i}. \tag{GUM.4}
$$

RMC.BestFit's general observation model augments this density likelihood with CDF, survival, interval-probability, threshold-count, and uncertain-observation convolution factors. With parameter trends, $z_i$ uses $\xi_i$ and $\alpha_i$ at the corresponding covariate index. The posterior multiplies the complete data likelihood by parameter and/or quantile priors. Default initial values and working bounds come from the Numerics distribution constraint routine and are sample-dependent.

Gumbel is the continuous $\kappa\to0$ limit of the RMC.Numerics GEV parameterization. Constraining the shape to zero reduces uncertainty but can materially bias rare-quantile extrapolation if the data support a bounded or heavy upper tail. Compare posterior predictive tail behavior and model criteria rather than treating the two-parameter model as automatically preferable.

## Compile-Checked API Example

<!-- snippet: distribution-gumbel -->
```csharp
private static (double Density, double Cdf, double Quantile) EvaluateGumbel()
{
    var distribution = new global::Numerics.Distributions.Gumbel(1500.0, 400.0);
    return Evaluate(distribution, value: 2200.0, nonExceedanceProbability: 0.99);
}
```

For annual maxima, the example's 0.99 nonexceedance quantile corresponds to a 0.01 annual exceedance probability under stationarity. It is not a guaranteed “100-year flood”; parameter uncertainty and nonstationarity must be propagated by the analysis.

## Validation and Limitations

The implementation is traced to `Numerics.Distributions.Gumbel`. Required numerical evidence includes $F(Q(p))=p$, density normalization, known moments, and convergence of GEV calculations as shape approaches zero. The current compile gate covers only API conformance. Block maxima must be approximately independent and represent comparable exposure; serial dependence, changing regulation, and mixture-generating mechanisms violate the simplest interpretation.

## References

<a id="ref-1"></a>[1] E. J. Gumbel, *Statistics of Extremes*. Columbia University Press, 1958.

<a id="ref-2"></a>[2] S. Coles, *An Introduction to Statistical Modeling of Extreme Values*. Springer, 2001.
