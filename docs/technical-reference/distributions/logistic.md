<!-- technical-reference-status: complete -->

# Logistic Distribution

[Distribution index](index.md) · [Generalized Logistic](generalized-logistic.md) · [Mixed-data likelihood](../data-frame/index.md)

## Purpose and Parameterization

The logistic distribution is a symmetric location-scale model with heavier tails than the Normal. RMC.Numerics uses constructor order $(\xi,\alpha)$, with location $\xi\in\mathbb R$ and scale $\alpha>0$ in observation units. It is not an extreme-value limit law for maxima, but it is the zero-shape limit of the implemented generalized-logistic family.

## Distribution

Let $z=(x-\xi)/\alpha$. For all real $x$,

$$
F(x)=\frac{1}{1+e^{-z}},\qquad
f(x)=\frac{e^{-z}}{\alpha(1+e^{-z})^2},\qquad
S(x)=\frac{1}{1+e^z}. \tag{LOGI.1}
$$

The quantile function is

$$
Q(p)=\xi+\alpha\log\frac{p}{1-p}. \tag{LOGI.2}
$$

Mean, median, and mode equal $\xi$; variance is $\pi^2\alpha^2/3$, skewness is zero, and ordinary kurtosis is $4.2$. Both tails decay exponentially, more slowly than Normal tails.

## Likelihood, Priors, and Estimation

For exact independent values,

$$
\ell(\xi,\alpha)=-n\log\alpha-\sum_i z_i
-2\sum_i\log(1+e^{-z_i}). \tag{LOGI.3}
$$

RMC.BestFit uses the same family density and CDF in exact, censored, uncertain, interval, threshold-count, stationary, and nonstationary contributions described in the data-frame chapter. Parameter and quantile priors multiply that full likelihood to form the posterior. Default starting values and finite optimizer/prior bounds are obtained from the sample through the Numerics constraint routine; only $\alpha>0$ is an intrinsic distribution constraint.

Location and scale are typically identifiable from moderate symmetric samples, but flood data can exhibit skewness that this family cannot represent. Heavy logistic tails may predict larger rare quantiles than a fitted Normal while still missing one-sided extreme-value tail structure. Distributional choice therefore requires more than center-of-sample fit.

## Compile-Checked API Example

<!-- snippet: distribution-logistic -->
```csharp
private static (double Density, double Cdf, double Quantile) EvaluateLogistic()
{
    var distribution = new global::Numerics.Distributions.Logistic(1500.0, 300.0);
    return Evaluate(distribution, value: 2200.0, nonExceedanceProbability: 0.99);
}
```

The location and scale are in the same discharge units as the evaluation value. The example exercises the distribution API; fitting, uncertainty propagation, and diagnostics belong to `UnivariateAnalysis`.

## Numerical Implementation and Validation

Stable evaluation in very large $|z|$ requires avoiding avoidable overflow in exponential expressions. Review evidence should include symmetry, $F(\xi)=1/2$, $Q(1-p)=2\xi-Q(p)$, moment checks, density normalization, and the generalized-logistic zero-shape limit. The compiled documentation test guards the constructor and member calls but does not replace those numerical tests.

## References

<a id="ref-1"></a>[1] N. L. Johnson, S. Kotz, and N. Balakrishnan, *Continuous Univariate Distributions*, vol. 2, 2nd ed. Wiley, 1995.
