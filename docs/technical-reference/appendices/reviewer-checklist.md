<!-- technical-reference-status: complete -->

# Scientific Reviewer Checklist

[Technical reference](../index.md) | [API traceability](../api-traceability.md) | [Verification evidence](verification-evidence.md) | [Review findings](../review-findings.md)

The [API traceability matrix](../api-traceability.md) enumerates every exported scientific type. This checklist groups those types by review unit and points to the governing formulation, compiled example, evidence, and known discrepancies.

| Review unit | Governing treatment | Key equations or definitions | Compiled example ID | Evidence and open findings |
|---|---|---|---|---|
| `IModel` and model infrastructure | [Models overview](../models/overview.md) | posterior/data/prior decomposition; pointwise units | `foundations-likelihood-decomposition` | Fast contract tests; decomposition findings linked by model |
| Data-frame types and series | [Mixed-observation likelihood](../data-frame/index.md) | exact, censoring, threshold count, and measurement convolution equations | `data-frame-mixed-observations` | Fast pointwise tests; TR-002 through TR-005 |
| Parameters, penalties, and quantile priors | [Parameters and priors](../models/parameters-and-priors.md) | bound, prior, penalty, and quantile-Jacobian mappings | `priors-single-quantile` | Fast mapping tests; TR-003, TR-004, TR-022 |
| Trend and link models | [Trend functions](../support/trend-functions.md), [link functions](../support/link-functions.md) | per-class formulas, inverses, and derivatives | `trend-linear-prediction`, `links-centered-log` | Fixed-value tests and serialization |
| Fifteen univariate families | [Distribution index](../distributions/index.md) and family chapters | support, PDF, CDF, quantile, moments, and sign conventions | `distribution-*` regions listed in family pages | [Distribution verification](../distributions/verification-matrix.md); Kappa TR-001 |
| Univariate model and analysis | [Univariate model](../distributions/univariate.md), [analysis](../analysis/univariate.md) | mixed likelihood and posterior quantile propagation | `fitting-analysis-workflow` | Fast lifecycle tests; TR-002 through TR-005, TR-013 |
| Point process/POT | [Point-process chapter](../distributions/point-process.md) | Poisson process intensity and exceedance likelihood | `point-process-workflow` | Threshold diagnostics; TR-009 through TR-012 |
| Mixture model | [Mixture chapter](../distributions/mixture.md) | latent-population likelihood and normalized weights | `mixture-workflow` | Composition tests; TR-006, TR-007 |
| Competing risks | [Competing-risks chapter](../distributions/competing-risks.md) | min/max CDF/PDF under selected dependence | `competing-risks-workflow` | Fixed composition tests; TR-008 |
| Composite/model averaging | [Composite chapter](../distributions/composite.md) | realization composition and criterion-weight formulas | `composite-model-average` | Weighting/lifecycle tests; TR-014 through TR-021 |
| Bulletin 17C | [Overview](../analysis/bulletin-17c.md), [estimation](../analysis/bulletin-17c-estimation.md), [uncertainty](../analysis/bulletin-17c-uncertainty.md) | EMA/GMM moments, penalties, covariance, and intervals | `bulletin17c-workflow` | Official standard and verification sources; TR-015 through TR-021, TR-026 |
| MLE and MAP | [MLE](../estimation/maximum-likelihood.md), [MAP](../estimation/maximum-a-posteriori.md) | bounded objectives, signs, covariance conventions | `maximum-likelihood-workflow`, `maximum-a-posteriori-workflow` | Fast state tests; TR-023 |
| GMM | [GMM chapter](../estimation/generalized-method-of-moments.md) | moment vector, weighting, Jacobian, identification | `gmm-workflow` | Fast tests plus verification sources; TR-026, TR-033, TR-034 |
| Bayesian MCMC | [MCMC chapter](../estimation/bayesian-mcmc.md) | sampler targets, proposals, saved-chain mapping | `bayesian-mcmc-workflow` | Pinned Numerics source; TR-024, TR-025, TR-029, TR-030 |
| Comparison and diagnostics | [Model comparison](../estimation/model-comparison.md), [diagnostics](../estimation/diagnostics.md), [influence](../estimation/influence-diagnostics.md), [predictive checks](../estimation/predictive-checks.md) | DIC, WAIC, PSIS-LOO, R-hat, ESS, influence, predictive \(p\) values | `model-comparison-workflow`, `bayesian-diagnostics-workflow`, `predictive-checks-workflow` | Fixed diagnostic tests; TR-024, TR-027 through TR-032 |
| Rating curves | [Rating-curve chapter](../analysis/rating-curve.md) | piecewise power law and base-10 error likelihood | `rating-curve-workflow` | Fixed prediction tests and verification sources; TR-042 through TR-045 |
| AR/MA/ARIMA/ARIMAX | [Time-series overview](../analysis/time-series.md) and four model chapters | conditional Gaussian likelihood, transforms, recursion, covariate ordering | `autoregressive-workflow`, `moving-average-workflow`, `arima-workflow`, `arimax-workflow` | Fast tests and verification sources; TR-035 through TR-046 |
| Bivariate copulas | [Bivariate chapter](../analysis/bivariate.md) | Sklar model, copula densities, pseudo-likelihood/IFM | `bivariate-workflow` | Pair and copula tests; TR-047 |
| Coincident frequency | [Coincident-frequency chapter](../analysis/coincident-frequency.md) | response inversion and copula-space bin integration | `coincident-frequency-workflow` | Numerical integration tests |
| Spatial extremes | [Spatial GEV chapter](../spatial/spatial-extremes.md) | GEV hierarchy, Gaussian copula, GP errors, full kernel, kriging | `spatial-gev-workflow` | Fast spatial tests and source audit; TR-048 through TR-062 |

## Release Questions

- Does every reported quantity identify units, probability convention, parameterization, and predictive unit?
- Does each likelihood include exactly the observation types claimed by the narrative?
- Are priors, penalties, and process densities separated and then recombined consistently?
- Are numerical outputs reproduced from a deterministic calculation, test assertion, or cited standard?
- Are MCMC seed, sampler, chain configuration, convergence criteria, and software versions reported?
- Are extrapolation, identifiability, missingness, and failure modes stated next to the result they affect?
- Are known production findings cited rather than silently documented as intended behavior?
- Are composite pairwise likelihood and other future enhancements clearly labeled unavailable?

## Release Commands

Run the following without invoking `RMC.BestFit.Verification`:

```powershell
dotnet build RMC.BestFit.sln -c Release --no-restore
dotnet test src\RMC.BestFit.Tests\RMC.BestFit.Tests.csproj -c Release --no-restore
powershell -ExecutionPolicy Bypass -File scripts\build-technical-reference-book.ps1
```

The book command checks the consolidated bibliography before generating `output/pdf/rmc-bestfit-technical-reference.pdf`. The full Verification project is user-run and belongs to the separate production-finding triage session.

---

[Technical reference](../index.md) | [API traceability](../api-traceability.md) | [Verification evidence](verification-evidence.md) | [Review findings](../review-findings.md)
