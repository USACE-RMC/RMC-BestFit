<!-- technical-reference-status: complete -->

# Verification and Evidence Matrix

[Technical reference](../index.md) | [Distribution verification](../distributions/verification-matrix.md) | [Review findings](../review-findings.md)

This matrix distinguishes evidence that passed the fast release gate from long-running verification sources that were inspected but not executed. It prevents a source file, published citation, or test name from being presented as a result that was actually reproduced in this session.

| Area | Deterministic/unit evidence | Long-running or external evidence | Release disposition |
|---|---|---|---|
| Model, parameter, trend, and link contracts | Fast constructor, mapping, likelihood-decomposition, and fixed-value tests; compiled examples | Primary statistical sources cited by chapter | Source-audited; fast gate passed |
| Mixed exact/censored/threshold/uncertain likelihood | Fast pointwise and data-frame tests | Historical/paleoflood literature; verification data sources | Source-audited; fast gate passed |
| Fifteen univariate distributions | Fixed PDF/CDF/quantile and parameter tests; exact API snippets | Theoretical, published-table, R/package, and report sources catalogued in [distribution verification](../distributions/verification-matrix.md) | Phase 1 closed; Kappa Four zero-shape TR-001 fixed and verified |
| POT/point-process | Threshold-diagnostic and lifecycle tests; compiled workflow | Estimator/threshold verification sources | Formulation complete; verification sources not run |
| Mixture and competing risks | Fixed composition, validation, and lifecycle tests | Recovery sources where present | Complete with registered findings |
| Bulletin 17C | Fast configuration/result tests, midpoint/ranked-initializer/bounds-repair regressions, and exact-LP3 Cohn scope guards | Seven formal worked-example GMM methods passed published mean/standard-deviation/skew parity at `1E-3`; fourteen ordinary/pivotal reliability cells produced 13,000 unguarded finite outputs with zero retries, substitutions, or exceptions | TR-016 through TR-021 closed in approved scopes; Cohn value verification and broader calibration/coverage remain deferred |
| MLE, MAP, and GMM | Objective-sign, bounds, state, profile, covariance, and compiled-workflow tests | R `bbmle` profile and self-checking R `gmm` fit/specification/covariance oracles | MLE/MAP profiling and GMM fixed-weight/two-step fit and covariance verified |
| Bayesian MCMC and diagnostics | Configuration, output mapping, threshold, and compiled-workflow tests | R `loo` PSIS parity and R `posterior` rank-normalized R-hat/ESS parity | Phase 2 closed; DIC/WAIC/PSIS and scoped MCMC diagnostics verified; TR-028 accepted limitation |
| Rating curves | Fixed prediction/validation and compiled-workflow tests | Rating-curve recovery sources and cited hydrometry standards | TR-042 criteria defect closed; TR-043 through TR-045 remain open |
| AR, MA, ARIMA, ARIMAX | Constructor, likelihood, recursion, transform, and compiled-workflow tests | Parameter recovery and forecasting verification sources | TR-042 criteria defect closed; TR-035 through TR-041 and TR-046 remain open |
| Bivariate copulas | Pair matching, likelihood, simulation shape, and compiled workflow | Copula recovery/integration sources and pinned Numerics tests | TR-047 criteria defect closed; broader recovery evidence remains planned |
| Coincident frequency | Dimension, monotonicity, integration, and compiled workflow | Numerical integration sources | Source-audited |
| Spatial GEV | Correlation, cached MVN, error model, result DTO, and compiled workflow tests | Spatial recovery/cross-validation sources | TR-055 prior-density correction closed; remaining TR-048 through TR-062 limitations stay open |
| Documentation | Namespace, local-link, citation-anchor, exported-API, and exact-snippet gates | PDF render and visual inspection | Required release gate |

## Evidence Labels

- **Fast gate passed** means the command reported in the release checklist completed with zero failures.
- **Source-audited** means equations and API behavior were reconciled against current BestFit and pinned Numerics source.
- **Verification source inspected** means the test logic and asserted comparison were reviewed, not executed.
- **Published evidence** means a chapter cites the official standard, paper, or book; it does not imply that every published number was independently recomputed.
- **Unavailable** means a production finding prevents a defensible interpretation until code and verification work are separately authorized.

## User-Run Verification

The full verification library is intentionally excluded from this release session because it can run for more than a day. The next verification-maintenance session should select narrow categories, update evidence alongside each production fix, and have the user execute the corresponding filtered command before a new validation claim is published.

---

[Technical reference](../index.md) | [Distribution verification](../distributions/verification-matrix.md) | [Review findings](../review-findings.md)
