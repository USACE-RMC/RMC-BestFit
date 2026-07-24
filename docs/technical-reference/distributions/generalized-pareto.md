<!-- technical-reference-status: complete -->

# Generalized Pareto Distribution

[Distribution index](index.md) · [Point-process analysis](point-process.md) · [Exponential limit](exponential.md)

## Purpose and Parameterization

The generalized Pareto distribution (GPD) models magnitudes above a high threshold. In RMC.Numerics its constructor order is $(\xi,\alpha,\kappa)$, where threshold/location $\xi$ and scale $\alpha>0$ have observation units and shape $\kappa$ is dimensionless. The implemented shape sign is opposite the common extreme-value convention: $\xi_{\mathrm{common}}=-\kappa$.

Set $z=(x-\xi)/\alpha$ and use the transform (GNO.1). The lower endpoint is $\xi$. If $\kappa\le0$, the upper endpoint is infinite; if $\kappa>0$, it is $\xi+\alpha/\kappa$. Consequently, negative Numerics shape denotes a heavy unbounded upper tail, zero shape is exponential, and positive shape is bounded above.

## Distribution

On the support,

$$
F(x)=1-e^{-y},\qquad
S(x)=e^{-y},\qquad
f(x)=\alpha^{-1}e^{-(1-\kappa)y}. \tag{GPD.1}
$$

Equivalently, for $\kappa\ne0$, $S(x)=(1-\kappa z)^{1/\kappa}$. The quantile is

$$
Q(p)=\begin{cases}
\xi+\dfrac{\alpha}{\kappa}\{1-(1-p)^\kappa\},&\kappa\ne0,\\
\xi-\alpha\log(1-p),&\kappa=0.
\end{cases} \tag{GPD.2}
$$

In this sign convention the mean exists for $\kappa>-1$ and the variance for $\kappa>-1/2$. Some current Numerics moment-property guards are more conservative; non-finite or unavailable displayed moments do not change the distribution formulas. `Lambda` stores the average number of peaks per block and is cloned with the distribution; it is exposure metadata for frequency conversion, not a fourth GPD shape parameter.

## Conditional and Point-Process Likelihoods

Given $n$ independent exceedance magnitudes above a fixed threshold, the conditional exact log likelihood is

$$
\ell(\xi,\alpha,\kappa)=-n\log\alpha-(1-\kappa)\sum_i y_i. \tag{GPD.3}
$$

This conditional likelihood does not by itself model how many exceedances occur. A peaks-over-threshold frequency analysis must combine magnitude and occurrence/exposure information; the point-process chapter gives that formulation. In a generic `UnivariateDistribution`, RMC.BestFit can also use GPD $f$, $F$, and $S$ for censored, uncertain, interval, and threshold-count observations. Priors and quantile priors multiply the full likelihood.

RMC.BestFit obtains data-dependent starts and bounds from the Numerics constraint routine. Threshold and location are not interchangeable without adjusting exceedances. Dependence between clustered peaks reduces effective information; declustering, exposure, seasonality, and threshold stability are analysis responsibilities.

## Compile-Checked API Example

<!-- snippet: distribution-generalized-pareto -->
```csharp
private static (double Density, double Cdf, double Quantile) EvaluateGeneralizedPareto()
{
    var distribution = new global::Numerics.Distributions.GeneralizedPareto(
        1000.0, 400.0, -0.15);
    return Evaluate(distribution, value: 1800.0, nonExceedanceProbability: 0.99);
}
```

The example uses a threshold of 1,000 discharge units and heavy-upper-tail Numerics shape $-0.15$. The returned 0.99 conditional quantile is not an annual 0.01-exceedance quantile until occurrence rate and exposure are incorporated.

## Validation and Limitations

Required evidence includes normalization, endpoint behavior, CDF/quantile inversion, the exponential limit, moment checks where finite, and GPD/GEV shape-sign parity. Point-process verification must separately validate count/exposure contributions. Tail estimates are highly threshold-sensitive: too low a threshold biases the asymptotic approximation; too high a threshold leaves too few exceedances.

## References

<a id="ref-1"></a>[1] A. A. Balkema and L. de Haan, “Residual life time at great age,” *Annals of Probability*, vol. 2, no. 5, pp. 792–804, 1974.

<a id="ref-2"></a>[2] J. Pickands III, “Statistical inference using extreme order statistics,” *Annals of Statistics*, vol. 3, no. 1, pp. 119–131, 1975.

<a id="ref-3"></a>[3] S. Coles, *An Introduction to Statistical Modeling of Extreme Values*. Springer, 2001.
