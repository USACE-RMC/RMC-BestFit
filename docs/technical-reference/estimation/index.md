<!-- technical-reference-status: complete -->

# Estimation, Comparison, and Diagnostics

[Technical Reference](../index.md) | [Models](../models/overview.md) | [Data likelihood](../data-frame/index.md)

BestFit is Bayesian-first: MCMC is the primary route for parameter and predictive uncertainty. MLE provides likelihood point estimates and local asymptotics; MAP provides a posterior mode; GMM is the only moments-based estimator and underlies the specialized Bulletin 17C path. These methods target different objectives and must not be interchanged merely because each returns a parameter vector.

## Chapter Map

| Chapter | Target | Primary output |
|---|---|---|
| [Maximum likelihood](maximum-likelihood.md) | `IModel.DataLogLikelihood` | MLE, curvature/sandwich covariance, AIC/BIC |
| [Maximum a posteriori](maximum-a-posteriori.md) | `IModel.LogLikelihood` | posterior mode and local Laplace curvature |
| [Generalized method of moments](generalized-method-of-moments.md) | moment quadratic plus optional penalty | GMM estimate and asymptotic covariance |
| [Bayesian MCMC](bayesian-mcmc.md) | full posterior kernel | joint posterior draws and credible uncertainty |
| [Model comparison](model-comparison.md) | likelihood or pointwise predictive criteria | AIC, BIC, DIC, WAIC, intended LOOIC |
| [Predictive checks](predictive-checks.md) | prior/posterior predictive simulation | discrepancy p-values and replicate summaries |
| [Diagnostics](diagnostics.md) | optimization and chain behavior | status, trace interpretation, R-hat, ESS |
| [Influence diagnostics](influence-diagnostics.md) | pointwise likelihood/moments/prior curvature | observation and prior sensitivity measures |

## Objective Crosswalk

For data log likelihood $\ell(\boldsymbol\theta)$, prior log kernel $r(\boldsymbol\theta)$, sample moment vector $\mathbf g_n$, weight $\mathbf W$, and optional penalty $P$:

$$
\begin{aligned}
\text{MLE:}&& \max_{\boldsymbol\theta}\;&\ell(\boldsymbol\theta),\\
\text{MAP:}&& \max_{\boldsymbol\theta}\;&\{\ell(\boldsymbol\theta)+r(\boldsymbol\theta)\},\\
\text{MCMC:}&& \boldsymbol\theta^{(s)}\sim&\;p(\boldsymbol\theta\mid\mathbf y)
\propto\exp\{\ell+r\},\\
\text{GMM:}&& \min_{\boldsymbol\theta}\;&\mathbf g_n^{\mathsf T}\mathbf W\mathbf g_n
\quad\text{or}\quad
\tfrac12\mathbf g_n^{\mathsf T}\mathbf W\mathbf g_n+P.
\end{aligned} \tag{EST.1}
$$

MLE and MAP require `IModel`. GMM requires `IGMMModel` or moment delegates and does not imply a likelihood. `Bulletin17CDistribution` is an `IGMMModel`, not an `IModel`; its uncertainty ensembles are frequentist/asymptotic or bootstrap products, not posterior chains.

## Required Reporting

Every fitted result should state:

- model and exact parameterization, units, support, fixed parameters, and bounds;
- objective decomposition and observation-information units;
- estimator/sampler, starts, seeds, tolerances, iterations, and dependency versions;
- termination and convergence evidence, including open diagnostic limitations;
- uncertainty construction and whether it is asymptotic, bootstrap, or posterior;
- predictive checks and sensitivity to scientifically material choices;
- verification source and tolerance for every numerical claim.

## Open Findings Affecting Interpretation

The canonical [review-findings register](../review-findings.md) is part of this reference. Phase 5 identified, among other items, false profile-likelihood naming, invalid PSIS tail smoothing, ARWMH adaptation concerns, NUTS acceptance-rate misreporting, nonstandard GMM J-statistic construction, covariance failure represented by zero matrices, and custom leverage quantities whose interpretation exceeds their derivation. The chapters describe implemented behavior, not aspirational behavior, and exclude affected values from scientific recommendations.

## Validation Boundary

All marked C# examples are compiled against the current .NET 10/RMC.BestFit 2.0 API and synchronized with source regions. Fast documentation, link, citation, namespace, and traceability gates run in `RMC.BestFit.Tests`. Long-running estimator recovery, coverage, and published-result comparisons remain in `RMC.BestFit.Verification`; Codex does not run that project.

---

[Start with MLE](maximum-likelihood.md) | [Bayesian MCMC](bayesian-mcmc.md) | [Diagnostics](diagnostics.md)
