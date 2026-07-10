# Influence Diagnostics

[<- Previous: Diagnostics](diagnostics.md) | [Back to Index](../../index.md) | [Next: Analyses Overview ->](../analysis/overview.md)

BestFit exposes influence diagnostics for Bayesian MCMC, maximum likelihood, maximum a posteriori estimation, and generalized method of moments. The key implementation rule is that diagnostics are computed from the model's actual pointwise likelihood methods, so exact observations, uncertain observations, interval data, and threshold data enter the diagnostics through the same likelihood decomposition used during estimation.

## Diagnostic Map

| Estimation Method | Diagnostics |
|-------------------|-------------|
| Bayesian MCMC | PSIS-LOO influence, Pareto `k`, MAP leverage decomposition, prior influence |
| Maximum Likelihood | Cook's distance, DFBETAS-like observation influence |
| Maximum A Posteriori | Cook's distance, DFBETAS-like observation influence, MAP leverage decomposition |
| Generalized Method of Moments | Cook's distance, observation influence, influence diagnostics |

## MAP Leverage

At the MAP estimate $\hat{\boldsymbol{\theta}}$, BestFit computes the posterior observed information from the negative Hessian of `Model.LogLikelihood`:

$$\mathbf{J}_{\text{post}} = -\nabla^2 \left[\log p(\mathbf{y}\mid\boldsymbol{\theta}) + \log p(\boldsymbol{\theta})\right]_{\boldsymbol{\theta}=\hat{\boldsymbol{\theta}}} \tag{1}$$

For observation $i$, the score vector is obtained by central differences on `Model.PointwiseDataLogLikelihood`:

$$\mathbf{g}_i = \nabla_{\boldsymbol{\theta}}\log p(y_i\mid\boldsymbol{\theta})\big|_{\hat{\boldsymbol{\theta}}}. \tag{2}$$

The fit-influence component is Cook's-distance-like:

$$D_i = \frac{1}{p}\mathbf{g}_i^\top\mathbf{J}_{\text{post}}^{-1}\mathbf{g}_i. \tag{3}$$

The variance-influence component uses the diagonal finite-difference information contribution $\mathbf{J}_i$ for the observation:

$$V_i = \frac{1}{p}\left|\operatorname{tr}\left(\mathbf{J}_{\text{post}}^{-1}\mathbf{J}_i\right)\right|. \tag{4}$$

The displayed observation leverage is:

$$\ell_i = D_i + V_i. \tag{5}$$

Prior components use the same fit-influence quadratic form with scores from `Model.PointwisePriorLogLikelihood`. For prior variance influence, BestFit removes one prior component from the posterior log likelihood, recomputes the local covariance, and reports the generalized-variance change. `LeverageDiagnostics` sums all observation and prior leverages and reports percentages of that computed total; it logs a debug warning if the total differs from the parameter count $p$ by more than 50 percent, but it does not rescale the values to force equality.

## Numerical Differentiation

The Hessian and score calculations route through `NumericalDiff` where possible. The initial step for parameter $j$ is:

$$h_j = \max\!\left(10^{-4}(|\theta_j| + 1),\;10^{-8}\right). \tag{6}$$

Flat finite-difference directions are escalated by a factor of 4 up to $10^{-2}$. This is important for hydrologic distributions with near-zero shape or skew parameters, where too-small perturbations can numerically land on the same approximation branch.

Diagonal Hessian entries use the three-point central formula:

$$H_{jj} = \frac{f(\boldsymbol{\theta}+h_j\mathbf{e}_j)-2f(\boldsymbol{\theta})+f(\boldsymbol{\theta}-h_j\mathbf{e}_j)}{h_j^2}. \tag{7}$$

Off-diagonal entries use the four-point cross-partial formula:

$$H_{jk} = \frac{f(\boldsymbol{\theta}+h_j\mathbf{e}_j+h_k\mathbf{e}_k)-f(\boldsymbol{\theta}+h_j\mathbf{e}_j-h_k\mathbf{e}_k)-f(\boldsymbol{\theta}-h_j\mathbf{e}_j+h_k\mathbf{e}_k)+f(\boldsymbol{\theta}-h_j\mathbf{e}_j-h_k\mathbf{e}_k)}{4h_jh_k}. \tag{8}$$

If direct inversion of $\mathbf{J}_{\text{post}}$ fails, `LeverageDiagnostics` applies `MatrixRegularization.MakeSymmetricPositiveDefinite` and retries the inversion.

## PSIS-LOO Influence

For posterior draw $s$ and data component $i$, BestFit forms raw log importance weights:

$$\log r_{is} = -\log p(y_i\mid\boldsymbol{\theta}^{(s)}). \tag{9}$$

Weights are shifted by their maximum log weight to avoid overflow. For each observation, the number of upper-tail weights used in Pareto smoothing is:

$$M = \min\!\left(\left\lfloor S/5 \right\rfloor,\left\lfloor 3\sqrt{S} \right\rfloor\right), \qquad 3 \le M \le S-1. \tag{10}$$

BestFit fits the upper tail with `Numerics.Distributions.GeneralizedPareto.MLE`. Numerics uses Hosking's shape parameter $\kappa$, whose sign is opposite the PSIS convention, so the reported diagnostic is:

$$\hat{k}_{\text{PSIS}} = -\hat{\kappa}_{\text{Numerics}}. \tag{11}$$

If the GPD MLE throws, the implementation falls back to a moment estimate from the tail weights. The final $k$ is clamped to $[-0.5, 1.5]$; smoothing is applied only when $-0.5 < k < 1$. Tail weights are replaced by GPD expected order-statistic quantiles:

$$Q(p_j)=\frac{\hat{\sigma}}{\hat{k}}\left[(1-p_j)^{-\hat{k}}-1\right],\qquad p_j=\frac{j+0.5}{M}. \tag{12}$$

When $|\hat{k}|<10^{-8}$, BestFit uses the exponential limit:

$$Q(p_j)=-\hat{\sigma}\log(1-p_j). \tag{13}$$

The pointwise expected log predictive density is:

$$\widehat{\operatorname{elpd}}_{\text{loo},i} = \log \sum_{s=1}^{S} w_{is}p(y_i\mid\boldsymbol{\theta}^{(s)}), \tag{14}$$

where $w_{is}$ are the smoothed and normalized importance weights.

## Aggregate LOO Statistics

`BayesianAnalysis` stores:

$$\operatorname{LOOIC} = -2\sum_i \widehat{\operatorname{elpd}}_{\text{loo},i}, \tag{15}$$

$$p_{\text{loo}} = \sum_i \operatorname{lppd}_i - \sum_i \widehat{\operatorname{elpd}}_{\text{loo},i}, \tag{16}$$

$$\operatorname{SE}(\operatorname{LOOIC}) = 2\sqrt{n\,\operatorname{Var}(\widehat{\operatorname{elpd}}_{\text{loo},i})}. \tag{17}$$

The variance in (17) is the sample variance across pointwise ELPD values with denominator $n-1$.

## Pareto K Categories

| Category | Rule | Interpretation |
|----------|------|----------------|
| Good | $k < 0.5$ | PSIS approximation is stable |
| OK | $0.5 \le k < 0.7$ | Moderate influence; monitor |
| Bad | $0.7 \le k < 1.0$ | High influence; exact LOO may be needed |
| Very Bad | $k \ge 1.0$ | Observation dominates the importance weights |

## MLE And MAP Influence

`MaximumLikelihood.GetCooksDistance()` computes equation (3) using the data-only Hessian from `Model.DataLogLikelihood`. `MaximumAPosteriori.GetCooksDistance()` uses the full posterior Hessian from `Model.LogLikelihood`.

Both `GetObservationInfluence()` methods return DFBETAS-like parameter influence:

$$\operatorname{DFBETAS}_{ij} = \frac{(\mathbf{J}^{-1}\mathbf{g}_i)_j}{\operatorname{SE}_j}, \tag{18}$$

where $\operatorname{SE}_j$ is the square root of the $j$th diagonal element of the covariance approximation.

## Data Components

Each observation's pointwise likelihood contribution comes from the model implementation:

| Type | Contribution |
|------|--------------|
| Exact | $\log f(y_i\mid\boldsymbol{\theta})$ |
| Uncertain | $\log \int f(q\mid\boldsymbol{\theta})g_i(q)\,dq$ evaluated by the model's quadrature routine |
| Interval | $\log [F(b_i\mid\boldsymbol{\theta}) - F(a_i\mid\boldsymbol{\theta})]$ |
| Threshold below | $n_{\text{below}}\log F(c_i\mid\boldsymbol{\theta})$ |
| Threshold above | $n_{\text{above}}\log[1-F(c_i\mid\boldsymbol{\theta})]$ |

Threshold data contributes one diagnostic row per threshold component with `Count` set to the number of represented years.

## Implementation Classes

| Class | Purpose |
|-------|---------|
| `InfluenceDiagnostics` | Container for PSIS-LOO results, Pareto `k`, ELPD-LOO, and observation metadata |
| `LeverageDiagnostics` | Computes and stores MAP leverage decomposition |
| `PriorInfluenceDiagnostics` | Summarizes prior components across thinned posterior samples |
| `BayesianAnalysis` | Entry points: `ComputeInfluenceDiagnostics()`, `ComputeLeverageDiagnostics()`, `ComputePriorInfluenceDiagnostics()` |
| `MaximumLikelihood` | MLE Cook's distance and DFBETAS-like influence |
| `MaximumAPosteriori` | MAP Cook's distance, DFBETAS-like influence, and leverage |
| `GeneralizedMethodOfMoments` | GMM Cook's distance and influence summaries |

## References

<a id="1">[1]</a> A. Vehtari, A. Gelman, and J. Gabry, "Practical Bayesian model evaluation using leave-one-out cross-validation and WAIC," *Statistics and Computing*, vol. 27, no. 5, pp. 1413-1432, 2017.

<a id="2">[2]</a> R. D. Cook, "Detection of influential observation in linear regression," *Technometrics*, vol. 19, no. 1, pp. 15-18, 1977.

<a id="3">[3]</a> B. C. Wei, Y. Q. Hu, and W. K. Fung, "Generalized leverage and its applications," *Scandinavian Journal of Statistics*, vol. 25, no. 1, pp. 25-36, 1998.

## Implementation Sources

Primary source paths: `src/RMC.BestFit/Estimation/BayesianAnalysis.cs`, `src/RMC.BestFit/Estimation/MaximumLikelihood.cs`, `src/RMC.BestFit/Estimation/MaximumAPosteriori.cs`, `src/RMC.BestFit/Estimation/GeneralizedMethodOfMoments.cs`, `src/RMC.BestFit/Estimation/NumericalDiff.cs`, `src/RMC.BestFit/Diagnostics/InfluenceDiagnostics.cs`, `src/RMC.BestFit/Diagnostics/LeverageDiagnostics.cs`, and `src/RMC.BestFit/Diagnostics/PriorInfluenceDiagnostics.cs`.

---

[<- Previous: Diagnostics](diagnostics.md) | [Back to Index](../../index.md) | [Next: Analyses Overview ->](../analysis/overview.md)
