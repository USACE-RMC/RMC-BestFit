<!-- verification-plan-status: active -->

# RMC.BestFit Verification Finalization Plan

This document is the durable handoff plan for finishing the RMC.BestFit 2.0 verification program. It is intentionally a work-control document: it records scope, sequencing, gates, off-ramps, and the current checkpoint so a future session can continue without relying on chat history.

The scientific source of truth remains the verification report chapters and the review-finding register. This plan tells the next worker where to look and how to proceed.

## Start Here

A new session should read these files in this order:

1. `docs/verification/verification-finalization-plan.md`
2. `docs/technical-reference/review-findings.md`
3. `docs/verification/README.md`
4. `docs/verification/model-estimation.md`
5. `docs/verification/test-inventory.md`
6. `verification/data/MANIFEST.md`
7. The relevant technical-reference chapter for the current finding

Current completed checkpoint: commit `4f91691 Verify MAP and GMM influence diagnostics`.

## Summary

The verification program is building a traceable numerical validation record for RMC.BestFit. The program began with repository integration, test ownership, external oracle infrastructure, and distribution fitting. Phase 2 is active and covers model estimation and diagnostics.

The report is a living Markdown book under `docs/verification/`. Every scientific claim must link to a test, oracle artifact, package/version, tolerance, result, review finding, and relevant technical-reference chapter. Markdown is the source of truth during development. PDF rendering is deferred until explicit release or visual-QA checkpoints.

The mandatory workflow for each finding is:

1. Construct an independent or analytical test.
2. Classify the finding as confirmed, rejected, accepted limitation, inconclusive, or waived with user-approved rationale.
3. If a defect is confirmed, stop and present a focused fix plan.
4. Implement an API-compatible fix only after approval.
5. Add fast regression coverage when production code changes.
6. Build and run the three unit-test projects after production changes.
7. Run only the focused verification command required for the claim.
8. Record the result in `review-findings.md`, the technical reference, and the verification report before continuing.

## Non-Negotiable Gates

- Preserve existing public method, constructor, property, event, interface, enum-value, and serialization signatures wherever possible.
- Preserve the public API baseline. A compatibility test detects removed or changed signatures and enum values; additive APIs require explicit review.
- If a breaking API change is scientifically unavoidable, stop and obtain approval for a compatibility and migration plan.
- Preserve unrelated modified and untracked user files.
- Never execute the complete Verification project or a broad verification filter.
- Focused verification must use `scripts/run-verification-test.ps1 -Test <fully-qualified-method-name>`.
- The verification runner must require one exact fully qualified method, place the MSTest filter after `--`, refuse class-only/wildcard/empty/multiple-match requests, require exactly one TRX result, and fail on zero matched tests.
- External R/Python tools generate committed oracle artifacts; C# verification tests never download packages and never require R or Python at runtime.
- A finding cannot be marked fixed until code, unit regression, focused numerical verification, finding text, report chapter, and technical-reference chapter agree.
- Do not compile or visually QA PDFs during ordinary development turns. Update Markdown only unless PDF QA is explicitly requested.
- Do not change production code in `RMC.BestFit`, `RMC.BestFit.UI`, `RMC.BestFit.App`, or `C:\GIT\Numerics` until the proposed fix has been explained and approved.
- For `C:\GIT\Numerics`, use only .NET 10 during this verification program.

## Verification Classification

Move tests into the correct project based on their role.

Unit/regression tests belong in the fast test projects when they cover constructors, property round-trips, validation, serialization, events, state transitions, exception paths, simple deterministic calculations, labels, or estimator smoke tests without an independent numerical oracle.

Verification tests belong in `RMC.BestFit.Verification` when they use an analytical solution, independently implemented algorithm, external package, published table, coverage study, scientifically meaningful numerical recovery test, or fitted diagnostic comparison.

Shared `TestData.cs` and `Datasets/` remain owned by `RMC.BestFit.Verification`. Unit test projects must use small inline fixtures and must not depend on verification datasets.

## Current Status

### Completed

- Phase 0 repository and documentation foundation is established.
- `RMC.BestFit.Verification` is included in `RMC.BestFit.sln`, included in Debug, and excluded from Release builds.
- Verification source is not git ignored.
- The guarded focused verification runner exists.
- External validation folder structure and manifests exist.
- Public API baseline tests exist.
- The review-finding register contains summary rows and detailed sections for TR-001 through TR-065.
- Phase 1 distribution fitting is complete for the currently scoped claims.
- Phase 2 fit influence, variance influence, and combined leverage for MAP/GMM are complete for the scoped Log10-Normal prior/penalty tests.

### Current Active Phase

Phase 2 - Model Estimation and Diagnostics.

Completed Phase 2 findings:

- TR-031: confirmed defect, fixed, passed analytical and R-parity evidence.
- TR-033: rejected non-defect, passed objective/gradient/covariance scaling evidence.
- TR-065: confirmed defect, fixed in the GMM diagnostic Hessian path only, passed R `gmm` parity.

Deferred Phase 2 items:

- TR-011, TR-022, TR-023, TR-024, TR-025, TR-026, TR-027, TR-028, TR-029, TR-030, TR-032, and TR-034.

Pareto-k, PSIS, LOO, DIC, WAIC, profile likelihood, Hansen J, covariance status, joint-prior sampling, and MCMC diagnostic parity remain active Phase 2 work.

## Phase 0 - Repository and Documentation Foundation

Status: substantially complete; keep these gates alive.

Required state:

1. `src/RMC.BestFit.Verification/RMC.BestFit.Verification.csproj` is restored to `RMC.BestFit.sln` under the `Verification` solution folder.
2. Debug builds include Verification; Release solution builds omit the Verification `Build.0` entry.
3. The assembly-wide `TestCategory("Verification")` is retained.
4. `src/RMC.BestFit.Verification/` is not blanket git ignored.
5. Legitimate verification source, datasets, scripts, and oracle artifacts are tracked. Generated `.vs`, `bin`, `obj`, `TestResults`, and similar files remain ignored.
6. Unrelated scratch files, especially `src/RMC.BestFit.Verification/extract_tests.py`, are left untouched unless the user explicitly decides otherwise.
7. Verification methods are inventoried and classified as unit/regression or verification.
8. Fast unit/regression tests are migrated out of Verification when they do not use scientific oracles.
9. `docs/verification/` contains the formal Markdown report structure.
10. `verification/r/`, `verification/python/`, and `verification/data/` contain external validation assets and oracle manifests.
11. `docs/technical-reference/review-findings.md` tracks all findings with severity, disposition, implementation status, verification status, evidence link, and last-reviewed date.

Initial migration targets included DTO/serialization portions of influence diagnostics, pointwise log likelihood, predictive checks, and nonnumerical fitting/GMM/MAP/MLE/Bayesian-analysis tests.

Off-ramp: stop if Verification cannot remain excluded from Release/normal gates, legitimate project files remain ignored, test ownership is ambiguous, or the public API baseline cannot reproduce the current public surface.

## Phase 1 - Distribution Fitting

Status: complete for the scoped Phase 1 claims.

Numerical verification requirements:

- Verify all 15 supported distribution families against independent implementations using fixed datasets, parameterization crosswalks, and maximum log-likelihood comparisons.
- Use analytical base-10 Log-Normal MLE as a primary closed-form case: with `x_i = log10(y_i)`, `mu_hat = mean(x)`, and `sigma_hat^2 = n^-1 sum((x_i - mean(x))^2)`.
- Verify parameters, data log likelihood, CDF, quantiles, and transformation Jacobian.
- Use R `lmomco::mle2par` for hydrologic families and SciPy for overlapping families, including documented Kappa Four parameterization.
- Preserve parameter conversions in oracle artifacts and documentation.
- Reproduce meaningful v1 datasets, replacing generic fit-succeeded assertions with parameter, likelihood, quantile, goodness-of-fit, and selected-model assertions.
- Verify FittingAnalysis ranking, AIC/BIC, RMSE, inverse-RMSE weights, partial failure, all-candidate failure, cancellation, and deterministic result ordering.

Resolved findings:

- TR-001: Kappa Four zero-shape PDF/CDF/quantile defect confirmed and fixed upstream in RMC.Numerics. Passed analytical verification.
- TR-002: finite Kappa shape validation rejected as non-defect. Passed finite-shape/support regression.
- TR-009: RMSE residual omission confirmed and fixed. Passed hand calculation and permutation tests.
- TR-010: all-candidate failure success state confirmed and fixed. Passed regression.
- TR-063: whole-series replacement plotting positions confirmed and fixed. Passed analytical and serialization regression.
- TR-064: DE/BFGS parameter tolerance concern rejected as non-defect. Passed SciPy parity using documented `1e-4` scaled parameter tolerance while retaining tight likelihood and criterion tolerances.

Phase exit criteria:

- All distribution families have named oracles and parameterization notes.
- TR-001, TR-002, TR-009, TR-010, TR-063, and TR-064 have final dispositions.
- Focused verification results and the distribution-fitting report chapter are complete.

## Phase 2 - Model Estimation and Diagnostics

Status: active.

### Log10-Normal Equivalence Experiment

Use deterministic symmetric data in log10 space plus an injected high-outlier variant.

Required checks:

1. Establish unpenalized MLE, MAP-with-flat-prior, and B17C GMM baselines.
2. Match a Gaussian prior on `mu` to the B17C quadratic penalty. Under the current penalty definition, begin with `MSE = tau_mu^2 / n` and confirm objective scaling through TR-033 evidence.
3. Exercise three prior regimes relative to the likelihood standard error:
   - wide and centered: `tau_mu = 100 SE_L`, no material location or variance effect;
   - narrow and centered: `tau_mu = 0.1 SE_L`, no location shift but large variance contraction;
   - narrow and shifted: `tau_mu = 0.1 SE_L`, center displaced by `2 SE_L`, producing location and variance influence.
4. Compare UnivariateDistribution MAP and B17CDistribution penalized GMM for `mu`, `sigma`, objective minimum, covariance, standard errors, design quantiles, and prior/penalty deletion displacement.
5. Validate observation fit influence using estimator-appropriate Cook-distance definitions.
6. Validate variance influence with analytical/full-Hessian Log10-Normal checks and deletion covariance.
7. Do not enforce a sum-to-p leverage rule unless derived and demonstrated.
8. Reserve Pareto-k exclusively for PSIS.
9. Keep MAP and GMM Cook magnitudes estimator-specific. They can each rank fit influence, but their absolute magnitudes are not cross-estimator equivalent because MAP is likelihood-based and GMM is least-squares on moments.
10. Use generalized variance for prior/penalty variance influence because it captures the joint covariance-volume effect and matched the planned prior-regime tests better than trace.

Completed for this experiment:

- MAP/GMM fit influence, variance influence, and combined leverage plot semantics are verified for the scoped Log10-Normal fixtures.
- The GMM diagnostic Hessian scale defect was fixed only in the diagnostic path.
- Quadratic penalties remain Gaussian priors; no objective, gradient, penalty Hessian, covariance, or estimator behavior changes were made for TR-033.

### External Model-Comparison Validation

Create deterministic posterior-draw and pointwise-log-likelihood fixtures so package comparisons do not depend on MCMC randomness.

Required external parity:

- DIC against R `BayesianTools`, including `D(theta_bar)`, `D_bar`, `p_D`, and the documented DIC convention.
- WAIC against R `loo::waic`.
- PSIS-LOO, LOOIC, `p_loo`, standard error, pointwise ELPD, and Pareto-k against R `loo`.
- Optional ArviZ agreement as a secondary implementation after the R oracle is stable.
- True profile likelihood by nuisance-parameter reoptimization against R `bbmle`, not coordinate slices.
- Overidentified GMM estimates, covariance, and Hansen J against R `gmm` and `specTest`.

### Phase 2 Findings

- TR-011: conventional AIC/BIC must be computed from maximized data likelihood; posterior criteria remain separate.
- TR-022: correct capability documentation to NUTS. Do not add plain HMC merely to match legacy wording.
- TR-023: MLE `ProfileLikelihood` must perform nuisance reoptimization. MAP coordinate-slice functionality must be explicitly named and must not be presented as frequentist confidence intervals. Preserve legacy signatures through wrappers where possible.
- TR-024: PSIS implementation must match reference-compatible cutoff excesses, stabilized GPD fitting, ordered expected statistics, and truncation.
- TR-025: upstream RMC.Numerics issue; covariance adaptation must update with every realized warmup state and use a documented diminishing/frozen schedule.
- TR-026: implement selected-weighting Hansen `J = n g^T W g` and chi-squared degrees of freedom.
- TR-027: add explicit covariance status/diagnostics and a `Try` path; existing public covariance methods must no longer silently return false zero uncertainty.
- TR-028: add optional joint-prior-sampling capability without changing `IModel`; coupled-prior models must implement it or report prior-predictive sampling as unavailable.
- TR-029: immediately correct diagnostic labels; then plan upstream rank-normalized split/folded R-hat and bulk/tail ESS with external parity.
- TR-030: add upstream NUTS-specific acceptance probability, divergence, tree-depth, and energy diagnostics; stop applying generic Metropolis thresholds to NUTS.
- TR-031: fixed. Unsupported hat-matrix claims were removed; combined leverage is an additive fit-plus-variance ranking index.
- TR-032: introduce a GMM-specific Cook/influence DTO and prevent Pareto-k labels, thresholds, and summaries from being applied.
- TR-033: rejected non-defect. The optimized GMM/penalty stack must preserve the Gaussian-prior inverse-variance weighting behavior. Do not change the gradient or penalty Hessian without tracing the full stack and obtaining approval.
- TR-034: permit overidentified one-step GMM when supplied a valid positive-definite weighting matrix.
- TR-065: fixed. GMM influence Hessian scale no longer depends on penalty presence.

Phase exit criteria:

- Log10-Normal prior/penalty behavior is demonstrated.
- MAP/GMM equivalence is resolved for the intended Gaussian-prior/quadratic-penalty questions.
- DIC, WAIC, and LOO have reproducible external artifacts.
- Every TR-011 and TR-022 through TR-034 finding has a disposition and linked evidence.

## Phase 3 - Data Handling and Bulletin 17C

Status: planned.

Findings and required direction:

- TR-003: define a chronology-invariant grouped nonstationary likelihood or explicitly require time-indexed thresholds; verify permutation invariance.
- TR-016: add estimator-neutral aliases/result metadata while retaining legacy Bayesian/MCMC-shaped members for serialization and source compatibility; correct UI/report terminology.
- TR-017: add `StudentizedPivotalBootstrap` as the accurate option name and retain `BiasCorrectedBootstrap` as an obsolete serialized alias with the same numeric value.
- TR-018: never substitute parent estimates for failed bootstrap fits. Use bounded attempts and fail the uncertainty run if the required accepted-replicate count is not reached.
- TR-019: compare truncated and untruncated bootstrap coverage and tail quantiles. Remove truncation if differences exceed Monte Carlo uncertainty; otherwise retain only as a named, reported robustification rule.
- TR-020: restrict Cohn-style diagnostics to LP3 through validation rather than silently applying base-10 transformations to other families.
- TR-021: create v2 evidence for official examples, PeakFQ/EMA diagnostics, covariance, penalty behavior, censoring patterns, refit failure, and uncertainty coverage. Do not cite the v1 EMA report as validation of the current GMM path.

Phase exit criteria:

- B17C terminology, bootstrap identity, failed-refit handling, truncation policy, diagnostic scope, and v2 evidence are coherent across code, tests, technical reference, and verification report.

## Phase 4 - Point Processes and Composite Models

Status: planned.

Findings and required direction:

- TR-004: separate empirical event count/rate from fitted threshold intensity and test mixed observation types.
- TR-005: generate counts, seasonal assignments, and marks from the fitted point-process intensity; verify Monte Carlo rates and conditional tails.
- TR-006: use an identified `K-1` simplex representation internally, never mutate proposals, and migrate legacy serialized `K`-weight models through a compatibility adapter.
- TR-007: implement a coherent mixed measure for an exact zero point mass, including CDF jump, quantiles, likelihood, and simulation.
- TR-008: fail EM when any required row has zero total component probability.
- TR-012: use dependency-aware competing-risk simulation and validate rank dependence for every supported mode.
- TR-013: reject or explicitly zero-weight invalid criteria; handle exact zero RMSE separately.
- TR-014: define separately fitted child posteriors as independent and combine them through seeded independent resampling invariant to chain ordering.
- TR-015: add a validated and serialized correlation-matrix property while retaining existing method signatures.

Phase exit criteria:

- Point-process simulation, mixture likelihoods, zero-inflation, composite weighting, posterior coupling, and dependency handling are internally coherent and verified against analytical or simulation fixtures.

## Phase 5 - Time-Series Models

Status: planned.

Findings and required direction:

- TR-035: correct pointwise Jeffreys component classification and scalar/pointwise identities.
- TR-036: fit transform parameters on training data only and prove holdout invariance.
- TR-037: correct raw/differenced index maps and test hand-computable `d=1` and `d=2` sequences.
- TR-038: simulate ARIMA on the transformed/differenced scale, integrate, then inverse-transform once.
- TR-039: keep ARIMAX regression and ARMA recursion on one model scale and inverse-transform only at the end.
- TR-040: apply identical invalid-scale guards to scalar and pointwise likelihoods.
- TR-041: align ARIMAX covariates and Jacobians by date and the exact differencing index map.
- TR-042: compute conventional AIC/BIC from MLE data likelihood for AR, MA, ARIMA, ARIMAX, and rating-curve analyses.
- TR-046: make transform updates atomic: rebuild transformed/differenced data, reset parameters/results, and either implement or compatibility-deprecate the unused offset.

Verification must include algebraic fixtures, scalar/pointwise decomposition, training/holdout isolation, seeded Monte Carlo moments, transformed simulation, and prior-invariance of conventional criteria.

Phase exit criteria:

- Time-series likelihood, forecasting, simulation, preprocessing, criteria, and pointwise decomposition behavior are consistent with the stated model scale and data indexing.

## Phase 6 - Rating Curve, Bivariate, and Spatial Models

Status: planned.

Findings and required direction:

- TR-043: include the base-10 change-of-variables term in discharge-space scalar and pointwise likelihoods.
- TR-044: enforce a defensible strictly positive exponent lower bound and test two-sided continuity.
- TR-045: validate only date-aligned likelihood pairs; report unrelated invalid records separately.
- TR-047: compute bivariate AIC/BIC from MLE data likelihood and retain Bayesian criteria for posterior comparison.
- TR-048: marginalize missing spatial sites with observed-site correlation submatrices cached by missingness pattern.
- TR-049: use row/year observed-data contributions for marginals and copula terms; classify latent Gaussian-process density as prior structure and enforce scalar/pointwise identities.
- TR-050: preserve completed cross-validation results across the restoration refit.
- TR-051: build an actual reduced training model for every held-out site.
- TR-052: pass held-out covariates consistently for location, scale, and shape trends.
- TR-053: record failed folds as failed/NaN, aggregate successful folds only, and report the success count.
- TR-054: use conditional Gaussian-process prediction per posterior draw and propagate conditional spatial variance.
- TR-055: suppress classical AIC/BIC where no defensible independent spatial observation unit exists; use explicitly defined posterior-predictive criteria instead.
- TR-056: fit each bootstrap replicate to its resampled data and reject incomplete replicate sets.
- TR-057: derive Hessian and score variability from the same estimating equations; report failure rather than substitute `J`.
- TR-058: compute regional statistics within each joint posterior draw before taking interval quantiles.
- TR-059: rename the current site-weight method as a weighting heuristic and remove composite-likelihood claims. Treat true pairwise composite likelihood and Godambe correction as a separately approved enhancement.
- TR-060: add explicit distance metric and coordinate-unit configuration, preserving Cartesian behavior by default and supporting validated geodesic coordinates additively.
- TR-061: simulate correlated normals through the fitted spatial correlation matrix and map them through site-specific inverse GEV CDFs.
- TR-062: make `RunAsync` dispatch the selected uncertainty method and record the method in result metadata.

Phase exit criteria:

- Rating-curve likelihoods, bivariate criteria, and spatial missingness/dependence/prediction/uncertainty workflows are mathematically explicit and verified against analytical, external, or simulation oracles.

## Recommended Phase 2 Batch Workflow

Phase 2 should not continue one issue at a time. Use read-only audits and then obtain batch approval.

### Batch 1 - Criteria, Profiles, GMM, and Covariance

Candidate findings:

- TR-011
- TR-022
- TR-023
- TR-026
- TR-027
- TR-034

First action: perform a read-only audit of the relevant code, tests, verification tests, technical-reference chapters, and oracle infrastructure. Produce an implementation ledger grouped into:

1. docs-only or wording-only changes;
2. API-compatible BestFit production fixes;
3. Numerics upstream fixes;
4. verification-oracle/test additions;
5. user decisions required before implementation.

Do not implement production fixes until the user approves the batch.

### Batch 2 - External Model-Comparison Oracles

Candidate work:

- DIC against R `BayesianTools`.
- WAIC against R `loo`.
- PSIS-LOO aggregate and pointwise values against R `loo`.
- Optional ArviZ parity after the R oracle is stable.

First action: create deterministic posterior-draw and pointwise-log-likelihood fixtures, generate committed oracle artifacts, and add exact focused C# verification methods.

### Batch 3 - PSIS Implementation

Candidate finding:

- TR-024

First action: prove the current BestFit PSIS behavior against pinned R `loo` fixtures. If it fails, present a focused algorithmic fix plan before editing production code.

### Batch 4 - Numerics MCMC Diagnostics

Candidate findings:

- TR-025
- TR-029
- TR-030

First action: audit `C:\GIT\Numerics` and BestFit integration using .NET 10 only. Separate documentation label corrections from sampler or diagnostic algorithm changes.

### Batch 5 - Joint Prior Sampling

Candidate finding:

- TR-028

First action: design an additive optional capability that does not change `IModel`. Coupled-prior models should implement the capability explicitly or report prior-predictive sampling as unavailable.

## Verification and Documentation Acceptance

- Deterministic algebra uses approximately `1e-10` absolute tolerance.
- Cross-language likelihood and criterion comparisons use `1e-8` absolute and `1e-7` relative tolerance unless the oracle manifest documents a stricter or algorithmically necessary exception.
- Optimizer parity uses scaled parameter tolerances no looser than `1e-5`, plus maximum-log-likelihood parity. When comparing objective-converged global optimizers with parameter-converged local optimizers, documented exceptions such as `1e-4` scaled parameter tolerance are allowed if likelihood parity and scientific conclusions are unaffected.
- PSIS totals target `1e-6`; Pareto-k targets `1e-4` against the pinned reference implementation.
- Coverage studies pass only when nominal coverage lies within the predeclared binomial confidence interval and the minimum successful-replicate count is met.
- Seeds, runtime versions, package versions, source commit, generation command, dataset hash, and tolerance rationale are recorded before observing C# results.
- Every confirmed production fix updates, in the same completed batch, its `review-findings.md` row and detailed section, corresponding technical-reference chapter, relevant verification-report chapter, and traceability table.
- Every core-library change passes Debug compilation, the XML/namespace documentation gate when available, `RMC.BestFit.Tests`, `RMC.BestFit.UI.Tests`, `RMC.BestFit.App.Tests`, public API compatibility, and serialization regression checks.
- Final conclusions are claim-specific. No subsystem is called verified merely because it compiled, converged, or reproduced its own prior output.

## Fresh-Session Prompt

Use this prompt to continue from a clean session:

```text
We are continuing RMC.BestFit verification finalization from commit 4f91691.

Read docs/verification/verification-finalization-plan.md first, then docs/technical-reference/review-findings.md, docs/verification/README.md, docs/verification/model-estimation.md, docs/verification/test-inventory.md, and verification/data/MANIFEST.md.

Do not compile PDFs unless I explicitly request PDF QA. Update Markdown source only.

Current scope: Phase 2 model estimation and diagnostics. Work in batches, not one finding at a time.

Constraints:
- Never run the full RMC.BestFit.Verification suite.
- You may run exact focused verification methods only.
- Run the three unit-test projects after any RMC.BestFit/UI/App code change.
- Do not change RMC.BestFit, UI, App, or Numerics production code until you first explain the proposed fix and I approve it.
- Preserve public API signatures wherever possible.
- Numerics reference should use local C:\GIT\Numerics, and Numerics work should use .NET 10 only.
- C# verification tests must use committed oracle artifacts, not R/Python at runtime.
- Markdown docs are the source of truth during development. Do not render PDFs unless requested.
- Write technical reference docs as current technical documentation, not as a historical issue log.
- Preserve unrelated modified/untracked files.

First task:
Perform a read-only Phase 2 audit for TR-011 and TR-022 through TR-034. Inspect existing code, unit tests, verification tests, docs, and oracle infrastructure. Produce a batch implementation ledger grouping findings into:
1. docs-only or wording-only,
2. BestFit API-compatible fixes,
3. Numerics upstream fixes,
4. verification-oracle/test additions,
5. items requiring my decision.

Do not implement production fixes until I approve the batch.
```

## Off-Ramps

Stop and ask the user before continuing if:

- a proposed fix requires a public API or serialization breaking change;
- an external package oracle disagrees with an analytical oracle and the discrepancy is not yet understood;
- a focused verification result exposes a production defect outside the approved batch;
- a Numerics fix is needed but the impact on BestFit cannot be isolated with local project references;
- a finding requires a scientific policy choice rather than an implementation correction;
- unrelated user changes overlap the files that must be edited.