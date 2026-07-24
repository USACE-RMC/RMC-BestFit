<!-- technical-reference-status: complete -->

# Generalized Extreme Value Distribution

[Distribution index](index.md) · [Gumbel limit](gumbel.md) · [Parameterization crosswalk](../appendices/parameterization-crosswalk.md)

## Purpose and Parameterization

The generalized extreme value (GEV) family is the limiting family for suitably normalized block maxima. RMC.Numerics uses constructor order $(\xi,\alpha,\kappa)$: location $\xi$ and scale $\alpha>0$ have observation units; shape $\kappa$ is dimensionless. Its sign is opposite the common Coles convention: $\xi_{\mathrm{Coles}}=-\kappa$. Confusing these conventions reverses whether the upper tail is heavy or bounded.

Define

$$
z=\frac{x-\xi}{\alpha},\qquad
y=\begin{cases}-\kappa^{-1}\log(1-\kappa z),&\kappa\ne0,\\z,&\kappa=0.\end{cases} \tag{GEV.1}
$$

The support requires $1-\kappa z>0$. Thus $\kappa<0$ gives a finite lower endpoint $\xi+\alpha/\kappa$ and an unbounded heavy upper tail; $\kappa=0$ is Gumbel; $\kappa>0$ gives a finite upper endpoint $\xi+\alpha/\kappa$.

## Distribution and Tail Behavior

On the support,

$$
F(x)=\exp[-e^{-y}],\qquad
f(x)=\alpha^{-1}\exp[-(1-\kappa)y-e^{-y}],\qquad S(x)=1-F(x). \tag{GEV.2}
$$

The quantile is

$$
Q(p)=\begin{cases}
\xi+\dfrac{\alpha}{\kappa}\{1-[-\log p]^\kappa\},&\kappa\ne0,\\
\xi-\alpha\log[-\log p],&\kappa=0.
\end{cases} \tag{GEV.3}
$$

In this sign convention the $r$th upper-tail moment exists when $\kappa>-1/r$. In particular, the mathematical mean exists for $\kappa>-1$ and variance for $\kappa>-1/2$. The current Numerics moment properties use more conservative shape checks in some regions; analysts should treat non-finite returned moments as an implementation limitation and use quantiles for tail interpretation.

## Full Likelihood and Posterior

For exact independent block maxima, substitute (GEV.1) into

$$
\ell(\xi,\alpha,\kappa)=-n\log\alpha
-(1-\kappa)\sum_i y_i-\sum_i e^{-y_i}, \tag{GEV.4}
$$

provided every observation lies on the parameter-dependent support; otherwise the likelihood is zero. RMC.BestFit extends (GEV.4) with family CDF/survival factors for censoring and thresholds, interval probabilities, and numerical measurement-error convolutions. Nonstationary location, scale, and shape are evaluated through their trend functions at each index. The posterior is the complete data likelihood times parameter, Jeffreys-scale, and/or quantile-prior contributions selected by the model.

## Estimation and Numerical Behavior

RMC.BestFit obtains sample-dependent starts and working bounds from `GeneralizedExtremeValue.GetParameterConstraints`; default uniform priors use the same returned bounds. Proposals crossing a support endpoint produce zero or non-finite probability and are rejected. Shape is often weakly identified from short records, and rare quantiles can be dominated by small changes in $\kappa$. A fitted finite endpoint close to observed maxima or a very heavy tail must be reported explicitly rather than hidden behind a return-period plot.

Near $\kappa=0$, Numerics switches to the analytic Gumbel branch using its `NearZero` tolerance, avoiding cancellation in (GEV.1) and (GEV.3). Extreme probabilities can still magnify floating-point and parameter uncertainty.

## Compile-Checked API Example

<!-- snippet: distribution-gev -->
```csharp
private static (double Density, double Cdf, double Quantile) EvaluateGev()
{
    var distribution = new global::Numerics.Distributions.GeneralizedExtremeValue(
        1500.0, 400.0, -0.10);
    return Evaluate(distribution, value: 2200.0, nonExceedanceProbability: 0.99);
}
```

The negative Numerics shape in this example denotes a heavy, unbounded upper tail. It corresponds to positive shape $0.10$ in the common Coles parameterization.

## Validation and Limitations

Evidence required for release includes density normalization, CDF/quantile inversion across all three support regimes, continuity at $\kappa=0$, endpoint behavior, theoretical moments where they exist, and parity with an independently parameterized implementation after applying the sign crosswalk. The compiled fixture guards the public API only. The asymptotic GEV argument does not guarantee that a particular block size is adequate or that maxima are independent and stationary.

## References

<a id="ref-1"></a>[1] S. Coles, *An Introduction to Statistical Modeling of Extreme Values*. Springer, 2001.

<a id="ref-2"></a>[2] J. R. M. Hosking and J. R. Wallis, *Regional Frequency Analysis: An Approach Based on L-Moments*. Cambridge University Press, 1997.
