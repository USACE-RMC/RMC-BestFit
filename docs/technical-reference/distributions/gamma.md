<!-- technical-reference-status: complete -->

# Gamma Distribution

[Distribution index](index.md) · [Mixed-data likelihood](../data-frame/index.md) · [Priors](../models/parameters-and-priors.md)

## Purpose and Parameterization

The Gamma family models positive, right-skewed magnitudes. RMC.Numerics uses constructor order $(\theta,k)$, where scale $\theta>0$ has observation units and shape $k>0$ is dimensionless. This order is easy to reverse because many texts write shape before scale and some APIs use rate $1/\theta$.

## Distribution

For $x>0$,

$$
f(x\mid\theta,k)=\frac{x^{k-1}e^{-x/\theta}}{\Gamma(k)\theta^k},\qquad
F(x)=P\!\left(k,\frac{x}{\theta}\right), \tag{GAM.1}
$$

where $P$ is the regularized lower incomplete gamma function. For $x\le0$, $F=0$. The survival and quantile functions are

$$
S(x)=Q\!\left(k,\frac{x}{\theta}\right),\qquad
Q_X(p)=\theta P^{-1}(k,p), \tag{GAM.2}
$$

with $Q(k,\cdot)$ the regularized upper incomplete gamma function. Moments are

$$
E[X]=k\theta,\quad \operatorname{Var}(X)=k\theta^2,\quad
\gamma_1=2/\sqrt{k},\quad \beta_2=3+6/k. \tag{GAM.3}
$$

The mode is $(k-1)\theta$ for $k>1$; the theoretical boundary mode is zero for $k\le1$, although the current Numerics `Mode` property returns `NaN` in that case. Shape $k=1$ gives an exponential distribution with zero location. All positive moments exist and the upper tail is exponentially bounded.

## Likelihood, Priors, and Estimation

For exact independent observations,

$$
\ell(\theta,k)=(k-1)\sum_i\log x_i-\theta^{-1}\sum_i x_i
-n\log\Gamma(k)-nk\log\theta. \tag{GAM.4}
$$

The mixed-data model replaces each exact factor with the appropriate CDF, survival, interval probability, measurement-error convolution, or threshold-count term. Nonstationary scale and shape are evaluated at each time or covariate index. The Bayesian posterior multiplies the resulting likelihood by configured parameter and quantile priors. RMC.BestFit obtains data-dependent initial values and working bounds from the Numerics constraint routine and uses those bounds for default uniform priors; positivity is the underlying distribution constraint.

Scale and shape are often strongly correlated in short samples. The mean/variance parameterization $m=k\theta$, $v=k\theta^2$ is useful for interpretation, but it is not the public API order. Data rounded to zero or containing negative values are incompatible unless their observation-error or censoring model assigns valid positive latent mass.

## Compile-Checked API Example

<!-- snippet: distribution-gamma -->
```csharp
private static (double Density, double Cdf, double Quantile) EvaluateGamma()
{
    var distribution = new global::Numerics.Distributions.GammaDistribution(500.0, 3.0);
    return Evaluate(distribution, value: 1800.0, nonExceedanceProbability: 0.99);
}
```

The example specifies a scale of 500 discharge units and dimensionless shape 3. It evaluates the distribution only; a defensible frequency analysis must fit parameters through an analysis and examine uncertainty and fit diagnostics.

## Numerical Implementation and Validation

CDF and quantile evaluation use Numerics incomplete-gamma routines. Review checks should cover normalization, limiting probabilities, moment simulation, $F(Q(p))\approx p$ over central and tail probabilities, and the $k=1$ exponential identity. The compiled example verifies the current constructor and method names, not numerical parity with another package. Because the support has a hard zero boundary, extrapolation can be especially sensitive when observations lie close to zero.

## References

<a id="ref-1"></a>[1] N. L. Johnson, S. Kotz, and N. Balakrishnan, *Continuous Univariate Distributions*, vol. 1, 2nd ed. Wiley, 1994.
