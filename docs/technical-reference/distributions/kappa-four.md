<!-- technical-reference-status: in-progress -->

# Kappa Four Distribution

[Distribution index](index.md) · [Scientific review findings](../review-findings.md) · [Parameterization crosswalk](../appendices/parameterization-crosswalk.md)

> **Completion blocker.** The theoretical family and the implemented general-case CDF are documented below, but the pinned RMC.Numerics 2.1.4 density and one inverse-CDF limiting branch are inconsistent at $\kappa=0$. See TR-001. This chapter cannot be classified complete until intended behavior is established and separately authorized production work is verified.

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

The current source instead evaluates $\xi-\alpha\log(1-p^h/h)$, which is generally different. The implemented and theoretical forms coincide for $h=1$.

The source reports the lower endpoint as $\xi+\alpha/\kappa$ for $h\le0,\kappa<0$; $\xi+(\alpha/\kappa)(1-h^{-\kappa})$ for $h>0,\kappa\ne0$; $\xi+\alpha\log h$ for $h>0,\kappa=0$; and $-\infty$ for $h\le0,\kappa\ge0$. The upper endpoint is infinite for $\kappa\le0$ and $\xi+\alpha/\kappa$ otherwise.

## Density, Moments, and Current Discrepancy

For $\kappa\ne0$ the implemented density is

$$
f(x)=\frac{1}{\alpha}u^{1/\kappa-1}F(x)^{1-h}. \tag{K4.5}
$$

The production `PDF` evaluates (K4.5) unchanged when $\kappa=0$. In .NET, the factor with base $u=1$ and infinite exponent evaluates as one, so the result omits the required $\exp[-(x-\xi)/\alpha]$ limiting factor and is generally finite but wrong. Together with the inverse-CDF parentheses error above, this is the confirmed [TR-001](../review-findings.md#tr-001) defect. A re-audit found that the broad shape validation is not itself defective: the support changes with $(\kappa,h)$, and all finite shape pairs define a Kappa distribution when $\alpha>0$; moment existence is a separate condition. The earlier validation concern is closed as [TR-002](../review-findings.md#tr-002).

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

The example deliberately uses nonzero shapes, avoiding the unresolved $\kappa=0$ branch. It demonstrates API shape, not suitability or numerical validation.

## Validation Required to Close the Chapter

Closure requires focused tests for density normalization, CDF/quantile inversion in all four branches, continuity as either shape tends to zero, all special-family identities, support validity, and comparison with Hosking's Kappa formulation. No validation claim is made for the affected limiting branch pending that work.

## References

<a id="ref-1"></a>[1] J. R. M. Hosking, “The four-parameter kappa distribution,” *IBM Journal of Research and Development*, vol. 38, no. 3, pp. 251–258, 1994.

<a id="ref-2"></a>[2] J. R. M. Hosking and J. R. Wallis, *Regional Frequency Analysis: An Approach Based on L-Moments*. Cambridge University Press, 1997.
