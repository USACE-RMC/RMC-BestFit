<!-- technical-reference-status: complete -->

# Pearson Type III Distribution

[Distribution index](index.md) · [Log-Pearson Type III](log-pearson-type-iii.md) · [Normal limit](normal.md)

## Purpose and Moment Parameterization

Pearson Type III (PIII) is a shifted, possibly reflected Gamma distribution parameterized in RMC.Numerics by its arithmetic mean $\mu$, standard deviation $\sigma>0$, and skewness $\gamma$. Constructor and model order is $(\mu,\sigma,\gamma)$. This user-facing moment parameterization is converted internally to

$$
a=\frac{4}{\gamma^2},\qquad
\beta=\frac12\sigma\gamma,\qquad
\xi=\mu-\frac{2\sigma}{\gamma}, \tag{P3.1}
$$

when $\gamma$ is not within the Numerics near-zero tolerance. Positive skew has support $[\xi,\infty)$; negative skew is reflected with support $(-\infty,\xi]$; zero skew uses the Normal limit.

## Distribution

Let $G_a$ be a unit-scale Gamma CDF. For $\gamma>0$,

$$
f(x)=\frac{(x-\xi)^{a-1}e^{-(x-\xi)/|\beta|}}
{|\beta|^a\Gamma(a)},\qquad
F(x)=G_a\left(\frac{x-\xi}{|\beta|}\right). \tag{P3.2}
$$

For $\gamma<0$, replace $x-\xi$ by $\xi-x$ in the density and use

$$
F(x)=1-G_a\left(\frac{\xi-x}{|\beta|}\right). \tag{P3.3}
$$

The quantile follows by the corresponding inverse incomplete-gamma function. At $\gamma=0$, $f$ and $F$ are Normal$(\mu,\sigma)$. Mean, standard deviation, and skewness are exactly the public parameters; ordinary kurtosis is $3+6/a=3+1.5\gamma^2$.

## Likelihood, Posterior, and Estimation

For positive skew and exact observations, omitting only notation already defined,

$$
\ell=\sum_i\left[-\frac{x_i-\xi}{|\beta|}+(a-1)\log(x_i-\xi)
-a\log|\beta|-\log\Gamma(a)\right]. \tag{P3.4}
$$

The reflected expression applies for negative skew. RMC.BestFit uses the family CDF and density in the complete mixed-data likelihood, including measurement-error convolution and threshold information. Parameter trends predict $(\mu_i,\sigma_i,\gamma_i)$; derived $(a_i,\beta_i,\xi_i)$ vary nonlinearly. Priors and quantile priors multiply the likelihood.

Starts and bounds are derived from sample product moments by the Numerics constraint routine and used for default uniform priors. As $\gamma\to0$, (P3.1) is ill-conditioned even though the distribution has a smooth Normal limit; Numerics therefore uses a tolerance branch. Skew is notoriously uncertain in short flood records and strongly affects rare quantiles.

## Compile-Checked API Example

<!-- snippet: distribution-pearson-iii -->
```csharp
private static (double Density, double Cdf, double Quantile) EvaluatePearsonTypeIII()
{
    var distribution = new global::Numerics.Distributions.PearsonTypeIII(
        1500.0, 400.0, 0.50);
    return Evaluate(distribution, value: 2200.0, nonExceedanceProbability: 0.99);
}
```

All but skew use observation units. Positive 0.50 skew creates a finite lower endpoint and unbounded upper tail.

## Validation and Limitations

Required evidence includes moment recovery, density normalization for both skew signs, CDF/quantile inversion, reflected-tail support, and continuity to Normal at zero skew. The compiled example guards the public parameter order. The shifted support can include negative values depending on the moments; physical plausibility and rare-tail leverage must be checked.

## References

<a id="ref-1"></a>[1] K. Pearson, “Contributions to the mathematical theory of evolution. II. Skew variation in homogeneous material,” *Philosophical Transactions of the Royal Society A*, vol. 186, pp. 343–414, 1895.
