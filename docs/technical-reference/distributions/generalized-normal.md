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

The zero-shape limit is Normal. Writing $Z\sim N(0,1)$ gives the useful generative identity $X=\xi+\alpha(1-e^{-\kappa Z})/\kappa$. The Normal moment-generating function, $E(e^{sZ})=e^{s^2/2}$, therefore gives

$$
E(X)=\xi+\frac{\alpha}{\kappa}(1-e^{\kappa^2/2}),\qquad
\operatorname{Var}(X)=\frac{\alpha^2}{\kappa^2}e^{\kappa^2}(e^{\kappa^2}-1).
\tag{GNO.6}
$$

These limits are $\xi$ and $\alpha^2$ at zero shape. Numerics evaluates analytical moments using stable exponential differences and logarithmic products; a nonrepresentable result can still overflow binary64. Differentiating the log density with respect to the latent Normal coordinate puts its maximum at $Z=\kappa$, so the mode is $\xi+\alpha(1-e^{-\kappa^2})/\kappa$, with limit $\xi$. This is an analytical mode, not a truncated numerical search.

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

The independent distribution-family evidence covers PDF, CDF, quantile, likelihood, and fitted-objective comparisons after reconciling the Hosking parameterization. The analytical identities above explain the moment and mode behavior; the compiled example separately checks API compatibility. See the [verification matrix](verification-matrix.md) for the current evidence and its acceptance rules.

The [distribution verification matrix](verification-matrix.md) identifies the current independent formula and fitted-objective comparisons, retained Bayesian/MLE recovery designs, and their distinct acceptance rules. The compiled example guards API compatibility; it does not run an estimator or establish scientific accuracy by itself.

## References

<a id="ref-1"></a>[1] J. R. M. Hosking and J. R. Wallis, *Regional Frequency Analysis: An Approach Based on L-Moments*. Cambridge University Press, 1997.
