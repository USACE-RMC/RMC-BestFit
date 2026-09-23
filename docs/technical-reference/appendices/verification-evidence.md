<!-- technical-reference-status: complete -->

# Verification Evidence Map

This appendix connects the methods explained in this manual to the preserved verification report. The current catalog contains 328 retained methods: 326 verified and two accepted limitations. The 56 retired coverage declarations are a separate historical set. Counts describe the evidence inventory, not the fraction of all possible scientific uses that is validated.

| Scientific area | Evidence and interpretation |
|---|---|
| [Observation processing](../../verification/report/time-series-data.md) | Missing-data and chronology contracts, import provenance, and independent processing references. |
| [Mixed observations and distribution families](../../verification/report/data-distributions-b17c.md) | Analytical and external formula/optimum comparisons, fifteen-family recovery, nonstationary responses, and published flood studies. |
| [Estimation and diagnostics](../../verification/report/estimation-diagnostics.md) | Conjugate posterior, profile likelihood, GMM objective/covariance, R loo and posterior diagnostics, and the two accepted limitations. |
| [Point process](../../verification/report/point-process-analysis.md) | Independent occurrence/magnitude generation, exposure and seasonal likelihoods, external point-process fit, and recovery. |
| [Mixtures](../../verification/report/mixture-analysis.md) | Independent scikit-learn Normal-mixture optimum; identified likelihood and retained Bayesian recovery designs. |
| [Competing risks](../../verification/report/competing-risk-analysis.md) | Gaussian rank/CDF identities and identified dominance designs: three MLE and two independent Bayesian fits. |
| [Composite and model averaging](../../verification/report/composite-analysis.md) | Closed forms, Gaussian orthants, R mistr, Cartesian posterior enumeration, and predictive recovery. |
| [Bivariate and coincident frequency](../../verification/report/bivariate-analyses.md) | Independent copula optima and recovery; Normal-sum and posterior-index oracles for response integration. |
| [Rating curves](../../verification/report/rating-curve.md) | Independent likelihood/optimum, continuity, and parameter/response recovery with a declared simultaneous grid band. |
| [Time series](../../verification/report/time-series-analyses.md) | Independent conditional objectives, optima, transforms, date alignment, forecasts, generation, and retained recovery designs. |
| [Spatial extremes](../../verification/report/spatial-extremes.md) | Observed-subset likelihood, correlation, geodesic distance, conditional GP prediction, Godambe, block bootstrap, and ten-site by 100-row recovery. |

## Reading an evidence claim

A fast contract guards deterministic software behavior. An analytical oracle derives a result independently; an external-package oracle freezes a named package calculation; a published reference supplies an official table or study. Recovery refits data from known parents under a declared experiment. Coverage requires repeated realizations and an acceptance rule for the nominal interval rate. These categories answer different questions.

The [catalog](../../verification/verification-catalog.json) supplies exact methods, source paths, sample units, oracle definitions, acceptance rules, and dispositions. The [artifact manifest](../../../verification/data/MANIFEST.md) preserves generator provenance. The report presents numerical targets and measured results with their units. This manual uses that existing evidence; editorial validation does not create new numerical runs-of-record.

## Boundaries that remain material

The generic prior sampler draws independent marginal priors and omits additional soft coupled-prior terms. One-step GMM influence is not calibrated to exact deletion magnitude. The current B17C diagnostic has 1,000 accepted refits, comprising 947 outer-converged and 53 outer-capped fits; this is reliability evidence, not interval coverage. The Kamp/Viglione systematic-only 1,000-year lower interval target remains 163 m³/s, while the published Skahill et al. (2016) Table 2 value is 183 m³/s.

The evidence does not establish arbitrary correlated competing-risk Bayesian recovery, rating-curve extrapolation, large-network or latent-error spatial recovery, universal sampler convergence, or broad Bulletin 17C coverage. A study must assess its own model, data, diagnostics, and sensitivity within these limits.
