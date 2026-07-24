<!-- technical-reference-status: complete -->

# Log-Pearson Type III Distribution

[Distribution index](index.md) · [Pearson Type III](pearson-type-iii.md) · [Bulletin 17C analysis](../analysis/bulletin-17c.md)

## Purpose and Federal-Frequency Parameterization

The Log-Pearson Type III (LP3) distribution models $Y=\log_{10}X$ as Pearson Type III. RMC.Numerics constructor order is $(\mu_Y,\sigma_Y,\gamma_Y)$: mean, standard deviation, and skewness of base-10 logarithms. BestFit uses the default `Base = 10`. These are not moments of discharge $X$. LP3 is the standard parent family in federal annual-peak flood-frequency practice, while BestFit's specialized Bulletin 17C path adds EMA/GMM, perception thresholds, regional skew, and outlier procedures beyond selecting this distribution.

For nonzero log skew define

$$
a=4/\gamma_Y^2,\qquad \beta=\sigma_Y\gamma_Y/2,\qquad
\xi_Y=\mu_Y-2\sigma_Y/\gamma_Y. \tag{LP3.1}
$$

## Distribution and Jacobian

Let $f_Y,F_Y$ be the Pearson III density and CDF from the preceding chapter. For $x>0$,

$$
f_X(x)=\frac{f_Y(\log_{10}x)}{x\ln10},\qquad
F_X(x)=F_Y(\log_{10}x),\qquad
Q_X(p)=10^{Q_Y(p)}. \tag{LP3.2}
$$

The Jacobian $(x\ln10)^{-1}$ is required for a likelihood on discharge scale. When $\gamma_Y=0$, LP3 reduces to the base-10 Log-Normal. For $\gamma_Y>0$, log space has finite lower endpoint $\xi_Y$, so $X$ has lower endpoint $10^{\xi_Y}$ and unbounded upper support. For $\gamma_Y<0$, the upper endpoint is $10^{\xi_Y}$ and the lower endpoint is zero.

Natural-space moments exist only when the Pearson-III moment-generating function is finite at the required multiple of $\ln10$. For example, the implemented nonzero-skew mean uses

$$
E[X]=\exp\{\xi_Y\ln10-a\log(1-\beta\ln10)\}, \tag{LP3.3}
$$

which requires $1-\beta\ln10>0`; the variance additionally requires $1-2\beta\ln10>0$. Quantiles remain the safer summary when these moments do not exist.

## Full Likelihood and Posterior

For exact $x_i>0$,

$$
\ell_X(\mu_Y,\sigma_Y,\gamma_Y)=
\sum_i\log f_Y(\log_{10}x_i)-\sum_i\log x_i-n\log(\ln10). \tag{LP3.4}
$$

RMC.BestFit combines LP3 density/CDF/survival values with exact, low-outlier, uncertain, interval, and perception-threshold contributions. In a general Bayesian univariate model, priors and quantile priors form the posterior. In the specialized Bulletin 17C analysis, estimation and penalties follow a separate algorithmic path and must not be inferred from the generic model alone.

Sample log moments initialize the Numerics constraint routine; its data-dependent bounds become default uniform-prior bounds in generic univariate analysis. Log skew, regional-skew information, low-outlier treatment, and historical thresholds can dominate rare-quantile estimates and must be reported together.

## Compile-Checked API Example

<!-- snippet: distribution-log-pearson-iii -->
```csharp
private static (double Density, double Cdf, double Quantile) EvaluateLogPearsonTypeIII()
{
    var distribution = new global::Numerics.Distributions.LogPearsonTypeIII(
        3.20, 0.25, 0.30);
    return Evaluate(distribution, value: 2200.0, nonExceedanceProbability: 0.99);
}
```

Parameters are moments of $\log_{10}$ discharge. This direct distribution call does not perform Bulletin 17C analysis.

## Validation and Limitations

Required evidence includes the transformation Jacobian, both skew signs, zero-skew Log-Normal limit, CDF/quantile inversion, support endpoints, and parity with official Bulletin 17C examples under the specialized analysis. The compile fixture supplies API evidence only. LP3 selection does not itself implement regional skew, EMA censoring, or Multiple Grubbs-Beck procedures.

## References

<a id="ref-1"></a>[1] U.S. Geological Survey, *Guidelines for Determining Flood Flow Frequency—Bulletin 17C*, Techniques and Methods, book 4, chap. B5, 2018.

<a id="ref-2"></a>[2] T. A. Cohn, W. L. Lane, and W. G. Baier, “An algorithm for computing moments-based flood quantile estimates when historical flood information is available,” *Water Resources Research*, vol. 33, no. 9, pp. 2089–2096, 1997.
