<!-- technical-reference-status: complete -->

# Generalized Normal Distribution

[Distribution index](index.md) · [Normal limit](normal.md) · [Parameterization crosswalk](../appendices/parameterization-crosswalk.md)

## Purpose and Parameterization

The implemented generalized Normal (GNO) applies the same logarithmic shape transformation used by the GEV/GLO families to a standard Normal variate. RMC.Numerics uses constructor order $(\xi,\alpha,\kappa)$ with $\alpha>0$. This is the Hosking generalized Normal used in frequency analysis, not every distribution called “generalized normal” or “exponential power” in other libraries.

Let $z=(x-\xi)/\alpha$ and

$$
y=\begin{cases}-\kappa^{-1}\log(1-\kappa z),&\kappa\ne0,\\z,&\kappa=0.\end{cases} \tag{GNO.1}
$$

The support requires $1-\kappa z>0$. Negative $\kappa$ gives a finite lower endpoint and unbounded upper tail; positive $\kappa$ gives a finite upper endpoint; zero shape gives all-real Normal support.

## Distribution

Using standard Normal density $\phi$ and CDF $\Phi$,

$$
F(x)=\Phi(y),\qquad
f(x)=\frac{1}{\alpha}\exp\left(\kappa y-\frac{y^2}{2}\right)\frac{1}{\sqrt{2\pi}}. \tag{GNO.2}
$$

The survival is $1-\Phi(y)$ and

$$
Q(p)=\begin{cases}
\xi-\dfrac{\alpha}{\kappa}\{\exp[-\kappa\Phi^{-1}(p)]-1\},&\kappa\ne0,\\
\xi+\alpha\Phi^{-1}(p),&\kappa=0.
\end{cases} \tag{GNO.3}
$$

The zero-shape limit is Normal. The transformed-Normal construction has finite moments on its valid support, but closed forms are not exposed by the current implementation. Numerics computes central moments by numerical integration with 1,000 intervals and caches them; it locates the mode with Brent optimization between the 0.001 and 0.999 quantiles. Those truncated numerical domains matter when the distribution is strongly skewed.

## Likelihood, Posterior, and Estimation

For an exact observation,

$$
\log f(x_i)=-\log\alpha+\kappa y_i-\frac{y_i^2}{2}-\frac12\log(2\pi). \tag{GNO.4}
$$

RMC.BestFit supplies (GNO.2)–(GNO.3) to the general likelihood for exact, uncertain, censored, interval, low-outlier, and threshold observations. Nonstationary parameters are evaluated at each observation index. Parameter and quantile priors form the posterior with the complete likelihood. Initial values and working bounds are produced from the exact sample by the Numerics constraint routine and become the default uniform prior bounds.

Shape-scale dependence and parameter-dependent endpoints can make optimization multimodal or produce invalid proposals. Numerics switches to the Normal branch for shape within its `NearZero` tolerance. Report the fitted endpoint and the sign convention whenever $\kappa\ne0$.

## Compile-Checked API Example

<!-- snippet: distribution-generalized-normal -->
```csharp
private static (double Density, double Cdf, double Quantile) EvaluateGeneralizedNormal()
{
    var distribution = new global::Numerics.Distributions.GeneralizedNormal(
        1500.0, 400.0, -0.10);
    return Evaluate(distribution, value: 2200.0, nonExceedanceProbability: 0.99);
}
```

The negative shape gives an unbounded upper tail under the implemented convention. The example does not assert that this family is appropriate for annual maxima; that is a data and model-selection question.

## Validation and Limitations

Required tests include normalization, CDF/quantile inversion, support endpoints, the Normal limit, and independent checks on numerically integrated moments. Strong-skew cases should specifically quantify error from the 1,000-interval moment calculation and the restricted mode-search interval. The documentation fixture only proves that the shown API calls compile.

## References

<a id="ref-1"></a>[1] J. R. M. Hosking and J. R. Wallis, *Regional Frequency Analysis: An Approach Based on L-Moments*. Cambridge University Press, 1997.
