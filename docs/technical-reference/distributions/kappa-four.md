<!-- technical-reference-status: complete -->

# Kappa Four Distribution

[Distribution index](index.md) · [Scientific review findings](../review-findings.md) · [Parameterization crosswalk](../appendices/parameterization-crosswalk.md)

## Purpose and Parameterization

The four-parameter Kappa family is a flexible location-scale-shape family that contains several distributions used in frequency analysis. RMC.Numerics constructor order is $(\xi,\alpha,\kappa,h)$, where $\alpha>0$; $\xi$ and $\alpha$ have observation units, while both shapes are dimensionless. Numerics calls the second shape `Hondo`.

Let $z=(x-\xi)/\alpha$ and $u=1-\kappa z$. For $\kappa h\ne0$, the implemented CDF is

$$
F(x)=\left[1-hu^{1/\kappa}\right]^{1/h}. \tag{K4.1}
$$

Both inner bases must be real and nonnegative. This imposes distributional support beyond the constructor's finiteness checks.

## Limiting Families and Quantiles

The CDF branches implemented by Numerics are

$$
F(x)=\begin{cases}
[1-hu^{1/\kappa}]^{1/h},&\kappa\ne0,\ h\ne0,\\
\exp[-u^{1/\kappa}],&\kappa\ne0,\ h=0,\\
[1-he^{-z}]^{1/h},&\kappa=0,\ h\ne0,\\
\exp[-e^{-z}],&\kappa=0,\ h=0.
\end{cases} \tag{K4.2}
$$

Thus $h=0$ is GEV, $(\kappa,h)=(0,0)$ is Gumbel, and $h=1$ is generalized Pareto under the matching location/scale/shape convention. For nonzero shapes, inversion gives

$$
Q(p)=\xi+\frac{\alpha}{\kappa}
\left[1-\left(\frac{1-p^h}{h}\right)^\kappa\right]. \tag{K4.3}
$$

For $\kappa=0,h\ne0$, mathematical inversion of (K4.2) gives

$$
Q(p)=\xi-\alpha\log\left(\frac{1-p^h}{h}\right). \tag{K4.4}
$$

The zero-primary-shape branch evaluates (K4.4) directly and is the algebraic inverse of the corresponding CDF branch.

The source reports the lower endpoint as $\xi+\alpha/\kappa$ for $h\le0,\kappa<0$; $\xi+(\alpha/\kappa)(1-h^{-\kappa})$ for $h>0,\kappa\ne0$; $\xi+\alpha\log h$ for $h>0,\kappa=0$; and $-\infty$ for $h\le0,\kappa\ge0$. The upper endpoint is infinite for $\kappa\le0$ and $\xi+\alpha/\kappa$ otherwise.

## Density and Moments

For $\kappa\ne0$ the implemented density is

$$
f(x)=\frac{1}{\alpha}u^{1/\kappa-1}F(x)^{1-h}. \tag{K4.5}
$$

For $\kappa=0$, the continuous density limit is $f(x)=\exp(-z)F(x)^{1-h}/\alpha$. The implementation evaluates this branch directly. Distribution support changes with $(\kappa,h)$, and all finite shape pairs define a Kappa distribution when $\alpha>0$; moment existence is a separate condition.

Numerics computes mean, standard deviation, skewness, and kurtosis by a cached 1,000-interval numerical central-moment calculation, and finds the mode by Brent maximization between the 0.001 and 0.999 quantiles. Tail-dominated moments and boundary modes require independent checks.

## Likelihood and Inference

Where $f$ and $F$ are finite and valid, the Kappa family enters the same exact, censored, uncertain, interval, threshold, stationary, and nonstationary likelihood as every `UnivariateDistribution`. The posterior multiplies that likelihood by configured priors. Initial values are obtained by an L-moment routine with a GEV fallback; optimizer bounds are then used for default uniform priors. Four flexible parameters create substantial confounding and multiple-optimum risk, especially in short records.

## Compile-Checked API Example

<!-- snippet: distribution-kappa-four -->
```csharp
private static (double Density, double Cdf, double Quantile) EvaluateKappaFour()
{
    var distribution = new global::Numerics.Distributions.KappaFour(
        1500.0, 400.0, 0.10, 0.20);
    return Evaluate(distribution, value: 2200.0, nonExceedanceProbability: 0.99);
}
```

The example uses nonzero shapes to illustrate the general branch. It demonstrates API shape, not suitability for a particular dataset.

## Validation

Analytical tests verify the zero-$\kappa$ density as the derivative of the CDF, CDF/quantile inversion, support, normalization, and two-sided continuity at the limiting branch. A finite-shape regression spans all four sign combinations of $(\kappa,h)$ and verifies admissibility, monotone quantiles, CDF/quantile round trips, positive interior density, and support endpoints. Independent full-family comparisons cover nonzero-shape behavior, zero-$h$ branches, special-family identities, and tail support against Hosking's formulation.

## References

<a id="ref-1"></a>[1] J. R. M. Hosking, “The four-parameter kappa distribution,” *IBM Journal of Research and Development*, vol. 38, no. 3, pp. 251–258, 1994.

<a id="ref-2"></a>[2] J. R. M. Hosking and J. R. Wallis, *Regional Frequency Analysis: An Approach Based on L-Moments*. Cambridge University Press, 1997.
