<!-- technical-reference-status: complete -->

# Weibull Distribution

[Distribution index](index.md) · [Exponential](exponential.md) · [Mixed-data likelihood](../data-frame/index.md)

## Purpose and Parameterization

The two-parameter Weibull family models positive magnitudes with a monotone hazard. RMC.Numerics uses constructor order $(\lambda,k)$: scale $\lambda>0$ has observation units and shape $k>0$ is dimensionless. The support begins at zero; there is no public location parameter in this family.

## Distribution

For $x\ge0$,

$$
f(x\mid\lambda,k)=\frac{k}{\lambda}\left(\frac{x}{\lambda}\right)^{k-1}
\exp\left[-\left(\frac{x}{\lambda}\right)^k\right],\qquad
F(x)=1-\exp\left[-\left(\frac{x}{\lambda}\right)^k\right]. \tag{WEI.1}
$$

The survival and quantile functions are

$$
S(x)=\exp[-(x/\lambda)^k],\qquad
Q(p)=\lambda[-\log(1-p)]^{1/k}. \tag{WEI.2}
$$

For $r>-k$, $E[X^r]=\lambda^r\Gamma(1+r/k)$. Hence

$$
E[X]=\lambda\Gamma(1+1/k),\quad
\operatorname{Var}(X)=\lambda^2\{\Gamma(1+2/k)-\Gamma(1+1/k)^2\}. \tag{WEI.3}
$$

The mode is zero for $k\le1$ and $\lambda[(k-1)/k]^{1/k}$ for $k>1$. At $k=1$ the family is exponential with location zero. The hazard decreases for $k<1$, is constant for $k=1$, and increases for $k>1$.

## Likelihood and Inference

For exact positive observations,

$$
\ell(\lambda,k)=n\log k-nk\log\lambda+(k-1)\sum_i\log x_i
-\sum_i(x_i/\lambda)^k. \tag{WEI.4}
$$

RMC.BestFit substitutes the Weibull density, CDF, and survival in the full mixed-data likelihood, including censoring, intervals, uncertain-observation convolution, and threshold counts. Parameter trends permit nonstationary $\lambda_i$ and/or $k_i$. The posterior combines all data contributions with parameter and quantile priors. Starts and working bounds are sample-dependent outputs of `Weibull.GetParameterConstraints`; both parameters must remain positive.

Short records may not distinguish scale from shape well, and zero observations interact with the boundary density: it diverges when $k<1$, equals $1/\lambda$ at $k=1$, and is zero when $k>1$. Rounded or censored zeros should therefore be represented by their observation process, not treated uncritically as exact continuous values.

## Compile-Checked API Example

<!-- snippet: distribution-weibull -->
```csharp
private static (double Density, double Cdf, double Quantile) EvaluateWeibull()
{
    var distribution = new global::Numerics.Distributions.Weibull(1500.0, 2.0);
    return Evaluate(distribution, value: 2200.0, nonExceedanceProbability: 0.99);
}
```

The scale and evaluated quantile carry the observation units. Shape 2 implies an increasing hazard; “hazard” must be interpreted in terms of the modeled random variable and sampling design, not automatically as chronological flood risk.

## Validation and Limitations

Required checks include density normalization, CDF/quantile inversion, theoretical moments, boundary values for shapes below/equal/above one, and the $k=1$ exponential identity. The compile gate verifies API conformance only. The zero lower endpoint and monotone-hazard shape may be implausible for regulated or mixed flood populations.

## References

<a id="ref-1"></a>[1] W. Weibull, “A statistical distribution function of wide applicability,” *Journal of Applied Mechanics*, vol. 18, pp. 293–297, 1951.
